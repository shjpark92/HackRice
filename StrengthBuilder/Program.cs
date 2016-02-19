using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Kinect;

namespace StrengthBuilder
{
    class Program
    {
        public static bool finished = false;
        private static bool isBodyCaptured;
        private static int frameCount;
        private static readonly object Lock = new object();

        private static Body[] bodies;
        private static BodyFrameReader bodyFrameReader;
        private static DateTime timeOfLastFrame = DateTime.Now;
        private static KinectSensor kinect = null;
        private static StreamWriter streamWriter;
        private static Timer timer;
        static System.Timers.Timer systemTimer;

        static void Main(string[] args)
        {
            var fileName = "kinect_data.csv";

            try
            {
                streamWriter = new StreamWriter(fileName);
                streamWriter.WriteLine("# ID, jointType, X, Y, Z, orientation.X, orientation.Y, orientation.Z, orientation.W, state");
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Error opening output file: " + e.Message);
                Close();
                return;
            }

            kinect = KinectSensor.GetDefault();

            if (kinect != null)
            {
                bodyFrameReader = kinect.BodyFrameSource.OpenReader();
                bodyFrameReader.FrameArrived += FrameArrived;

                try
                {
                    kinect.Open();
                }
                catch(IOException)
                {
                    kinect = null;
                }
            }

            Console.WriteLine($"{DateTime.Now:T}: Writing data to: {fileName}. This program will execute for 60 seconds. Press X to terminate the program.");

            timeOfLastFrame = DateTime.Now;

            systemTimer = new System.Timers.Timer();
            systemTimer.AutoReset = false;
            systemTimer.Elapsed += new System.Timers.ElapsedEventHandler(t_Elapsed);
            systemTimer.Interval = ((60 - timeOfLastFrame.Second) * 1000 - timeOfLastFrame.Millisecond);
            systemTimer.Start();

            timer = new Timer(o =>
            {
                double framesPerSecond;
                var currentTime = DateTime.Now;
                lock (Lock)
                {
                    framesPerSecond = frameCount / (currentTime - timeOfLastFrame).TotalSeconds;
                    frameCount = 0;
                    timeOfLastFrame = currentTime;
                }
                Console.Write($"\rAcquiring at {framesPerSecond:F1} fps. Tracking {(isBodyCaptured ? 1 : 0)} body.");
            }, null, 1000, 1000);


            Console.CancelKeyPress += ConsoleHandler;
            while (true)
            {
                var userInput = Console.ReadKey(true);

                if (userInput.Key == ConsoleKey.X)
                {
                    break;
                }
            }
            Console.WriteLine(Environment.NewLine + $"{DateTime.Now:T}: Stoping capture");
            Close();
        }

        static void t_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            Console.WriteLine(DateTime.Now.ToString("o"));
            finished = true;
            Close();
        }

        private static void ConsoleHandler(object sender, ConsoleCancelEventArgs args)
        {
            Console.WriteLine(Environment.NewLine + $"{DateTime.Now:T}: Stoping capture");
            Close();
        }

        private static void Close()
        {
            timer?.Change(Timeout.Infinite, Timeout.Infinite);

            if (bodyFrameReader != null)
            {
                bodyFrameReader.Dispose();
                bodyFrameReader = null;
            }

            if (kinect != null)
            {
                kinect.Close();
                kinect = null;
            }

            if (streamWriter != null)
            {
                streamWriter.Close();
                streamWriter = null;
            }
        }

        static void FrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            var dataReceived = false;
            var time = new TimeSpan();

            using (var bodyFrame = e.FrameReference.AcquireFrame())
            {
                if (bodyFrame != null)
                {
                    time = bodyFrame.RelativeTime;
                    if (bodies == null)
                    {
                        bodies = new Body[bodyFrame.BodyCount];
                    }

                    bodyFrame.GetAndRefreshBodyData(bodies);
                    dataReceived = true;
                }
            }

            if (dataReceived)
            {
                var body = bodies.FirstOrDefault(b => b.IsTracked);
                lock (Lock)
                {
                    frameCount += 1;
                    isBodyCaptured = body != null;
                }
                if (body != null)
                {
                    CommitData(time, body);
                }
            }
        }

        static void CommitData(TimeSpan timestamp, Body body)
        {
            var shoulder = new List<Joint>();
            var handtip = new List<Joint>();
            var handright = new List<Joint>();
            var elbowright = new List<Joint>();
            var joints = body.Joints;
            var orientations = body.JointOrientations;
            var collectionID = 0;
            foreach (var jointType in joints.Keys)
            {
                var jointString = jointType.ToString();
                // shoulderRight; handTipRight, handRight, elbowRight
                if (jointString == "ShoulderRight")
                {
                    shoulder.Add(joints[jointType]);
                }
                    
                if (jointString == "HandTipRight")
                {
                    handtip.Add(joints[jointType]);
                }
                    
                if (jointString == "HandRight")
                {
                    handright.Add(joints[jointType]);
                }
                    
                if (jointString == "ElbowRight")
                {
                    elbowright.Add(joints[jointType]);
                }
                var position = joints[jointType].Position;
                var orientation = orientations[jointType].Orientation;
                streamWriter.WriteLine("{0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}",
                    collectionID,
                    jointType,
                    position.X, position.Y, position.Z,
                    orientation.X, orientation.Y, orientation.Z, orientation.W,
                    joints[jointType].TrackingState);


                SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();

                builder["Server"] = "hackrice.database.windows.net";
                builder["User ID"] = "strengthbuilder";
                builder["Password"] = "hRsbdb16";
                builder["Database"] = "HackRice";
                builder["Integrated Security"] = false;
                builder["Encrypt"] = true;

                try
                {
                    using (SqlConnection connection = new SqlConnection(builder.ToString()))
                    {
                        SqlCommand command = new SqlCommand();
                        command.Connection = connection;
                        command.CommandTimeout = 10;
                        command.CommandType = System.Data.CommandType.Text;

                        StringBuilder sqlCommand = new StringBuilder("INSERT INTO dbo.data ");
                        sqlCommand.Append("(collectionid, jointType, xCoord, yCoord, zCoord, xOrientation, yOrientation, zOrientation, wOrientation, TrackingState) ");
                        sqlCommand.AppendFormat("VALUES({0}, '{1}', {2}, {3}, {4}, {5}, {6}, {7}, {8}, '{9}');", collectionID,
                                                jointType,
                                                position.X, position.Y, position.Z,
                                                orientation.X, orientation.Y, orientation.Z, orientation.W,
                                                joints[jointType].TrackingState);

                        command.CommandText = sqlCommand.ToString();
                        connection.Open();

                        SqlDataReader reader = command.ExecuteReader();
                        Console.WriteLine("Pushed to Azure");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("An error occured: " + ex.Message);
                }

            }

            var previousInterest = shoulder.First();
            var index = 0;

            foreach (var interest in shoulder)
            {
                float distance = (float)Math.Sqrt(Math.Pow(interest.Position.X - previousInterest.Position.X, 2) + Math.Pow(interest.Position.Y - previousInterest.Position.Y, 2) + Math.Pow(interest.Position.Z - previousInterest.Position.Z, 2));
                previousInterest = interest;

                SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();

                builder["Server"] = "hackrice.database.windows.net";
                builder["User ID"] = "strengthbuilder";
                builder["Password"] = "hRsbdb16";
                builder["Database"] = "HackRice";
                builder["Integrated Security"] = false;
                builder["Encrypt"] = true;

                try
                {
                    using (SqlConnection connection = new SqlConnection(builder.ToString()))
                    {
                        SqlCommand command = new SqlCommand();
                        command.Connection = connection;
                        command.CommandTimeout = 10;
                        command.CommandType = System.Data.CommandType.Text;

                        StringBuilder sqlCommand = new StringBuilder("INSERT INTO dbo.analyticsResults ");
                        sqlCommand.Append("(entryid, eucDistance, bodyPart) "); 
                        sqlCommand.AppendFormat("VALUES({0}, {1}, '{2}');", index, distance, "ShoulderRight");

                        command.CommandText = sqlCommand.ToString();
                        connection.Open();

                        SqlDataReader reader = command.ExecuteReader();
                        Console.WriteLine("Pushed to Azure");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("An error occured: " + ex.Message);
                }
                index++;
            }
        }
    }
}
