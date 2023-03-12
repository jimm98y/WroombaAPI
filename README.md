# RoombaAPI
A client to control the iRobot Roomba 966 vacuum cleaner. 

## Disclaimer
To use this unofficial client, it is recommended to NOT update the firmware of the robot to the latest version and stay on version 2.x.x. Newer versions of the firmware are neither tested nor supported. 

## Discovery
Discover robots in the local network:
```
var robots = await RoombaClient.DiscoverAsync("192.168.1.255");
```
Get the robot:
```
var robot = robots.First();
```
## RoombaClient
Create the `RoombaClient` and subscribe for the message received callback:
```
var roombaClient = new RoombaClient(robot.HostName);
roombaClient.MessageReceived += roombaClient_MessageReceived;
roombaClient.SignIn(robot.UserName, robot.Password);
```
Handle incoming messages:
```
private void roombaClient_MessegeReceived(object? sender, MessageReceivedEventArgs e)
{
    var state = e.State;
    if (state != null && state.State != null && state.State.Reported != null)
    {
        if (state.State.Reported.CleanSchedule != null)
        {
            var cycle = state.State.Reported.CleanSchedule.Cycle;
            ...
        }
    }
}
```
Control Roomba:
```
roombaClient.Start();
```
# Credits
This project is just a C# port of https://github.com/koalazak/dorita980.