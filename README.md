# Hardware to VRChat OSC Reporter

This is a simple tool that could constantly sends hardware status of your computer rig to VRChat via OSC channels, so that you can synchronize these status to your own avatars. It is powered by [Libre Hardware Monitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) (A fork of Open Hardware Monitor). It supports every hardware what Libre Hardware Monitor supports, including loads, temperatures of CPU, GPU, RAM, fan, water cooling, etc. Additionally, it can be also configurated to send datetime information, useful for gadget like watches.

To use it, just double-click the exe-file, and it will appears as a console window. If there's no config file, it will generate one for you. Here are the available options in the config file:
- `ipAddress`: The IP address of your VRChat client, if it is the same machine, leave it `127.0.0.1`
- `port`: The OSC listening port of your VRChat client, default is `9000`, no need to change it unless you changed that for your client.
- `updateInterval`: The interval between updates in milliseconds, default is `1000`
- `addresses:`: You can mix and match all hardware channels with math expressions like this:
  ```yaml
    /avatar/parameters/something_fancy: '(intelcpu.0.load.1 + intelcpu.0.load.2) / 2'
  ```
  Above will send cpu #1 and #2 average load to `something_fancy` avatar parameter.  
  It also supports following math functions (case insensitive):  
    - `abs`
    - `sqrt`
    - `cbrt`
    - `pow`
    - `lerp`
    - `remap`
    - `saturate`
    - `sign`
    - `round`
    - `floor`
    - `ceil`
    - `trunc`
    - `sin`
    - `cos`
    - `tan`
    - `asin`
    - `acos`
    - `atan`
    - `sinh`
    - `cosh`
    - `tanh`
    - `asinh`
    - `acosh`
    - `atanh`
    - `log`
    - `exp`
    - `log10`
    - `log2`
    - `random`
    - `isnan`
    - `switch`
  And these variables (case insensitive):
    - `LocalTime.Year`
    - `LocalTime.Month`
    - `LocalTime.Day`
    - `LocalTime.DayOfWeek`
    - `LocalTime.TimeOfDay`
    - `LocalTime.Timestamp`
    - `UtcTime.Year`
    - `UtcTime.Month`
    - `UtcTime.Day`
    - `UtcTime.DayOfWeek`
    - `UtcTime.TimeOfDay`
    - `UtcTime.Timestamp`

As an another example, here is how you can make it send [VRCWatch](https://github.com/mezum/vrcwatch)-Compatible OSC messages:
```yaml
skipAdminCheck: true
ipAddress: 127.0.0.1
port: 9000
updateInterval: 500
addresses:
  /avatar/parameters/DateTimeYear: LocalTime.Year
  /avatar/parameters/DateTimeMonth: LocalTime.Month
  /avatar/parameters/DateTimeDay: LocalTime.Day
  /avatar/parameters/DateTimeHour: floor(LocalTime.TimeOfDay * 24)
  /avatar/parameters/DateTimeMinute: floor(LocalTime.TimeOfDay * 1440 % 60)
  /avatar/parameters/DateTimeSecond: floor(LocalTime.TimeOfDay * 86400 % 60)
  /avatar/parameters/DateTimeHourF: floor(LocalTime.TimeOfDay * 24) / 24
  /avatar/parameters/DateTimeMinuteF: floor(LocalTime.TimeOfDay * 1440 % 60) / 60
  /avatar/parameters/DateTimeSecondF: floor(LocalTime.TimeOfDay * 86400 % 60) / 60
  /avatar/parameters/DateTimeDayTime: LocalTime.TimeOfDay
  /avatar/parameters/DateTimeHourFA: LocalTime.TimeOfDay
  /avatar/parameters/DateTimeMinuteFA: LocalTime.TimeOfDay * 1440 % 60 / 60
  /avatar/parameters/DateTimeSecondFA: LocalTime.TimeOfDay * 86400 % 60 / 60
```

## License

[MIT](LICENSE)