//Flot Line Chart
//$(document).ready(function() {
jQuery(document).ready(function ($) {
    console.log("document ready");

    var xmlHttp = new XMLHttpRequest();
    xmlHttp.open( "GET", "http://sbapi.azurewebsites.net/tables/analyticsResults", false ); // false for synchronous request
    xmlHttp.setRequestHeader("ZUMO-API-VERSION","2.0.0");
    xmlHttp.send( null );
    var payload = xmlHttp.responseText;
    payload = JSON.parse(payload);

    plot();

    function plot() {
        // shoulderRight; handTipRight, handRight, elbowRight
        var hand = [],
            arm = [];
        var i = 0;
        var j = 0;
        hand.push([i, 0]);
        arm.push([j, 0]);

        i++;
        j++;
        for (var point in payload) {
            point = payload[point];
            if((point.bodyPart == "HandTipRight") || (point.bodyPart == "HandRight")) {
                 hand.push([i, point.eucDistance]);
                 i++;
            }
            if ((point.bodyPart == "ShoulderRight") || (point.bodyPart == "ElbowRight")) {
                arm.push([j, point.eucDistance]);
                j++;
            }
        }

        var options = {
            series: {
                lines: {
                    show: true
                },
                points: {
                    show: true
                }
            },
            grid: {
                hoverable: true //IMPORTANT! this is needed for tooltip to work
            },
            yaxis: {
                min: 0,
                max: 1
            },
            tooltip: true,
            tooltipOpts: {
                content: "'%s' of %x.1 is %y.4",
                shifts: {
                    x: -60,
                    y: 25
                }
            }
        };

        var plotObj = $.plot($("#flot-line-chart"), [{
                data: arm,
                label: " Hand Movement"
            }, {
                data: hand,
                label: " Arm Movement"
            }],
            options);
    }
});
    
