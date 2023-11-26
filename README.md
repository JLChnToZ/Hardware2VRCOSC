# Hardware to VRChat OSC Reporter

This is a simple tool that could constantly sends hardware status of your computer rig to VRChat via OSC channels, so that you can synchronize these status to your own avatars. It is powered by [Open Hardware Monitor](https://openhardwaremonitor.org/). It supports every hardware what Open Hardware Monitor supports, including loads, temperatures of CPU, GPU, RAM, fan, water cooling, etc. Additionally, it can be also configurated to send datetime information, useful for gadget like watches.

To use it, just double-click the exe-file, and it will appears as a console window. You can then copy the listed OSC channel path of your desired hardware information to the input section of parameters in the [config json file of your avatar](https://docs.vrchat.com/docs/osc-avatar-parameters) (assume you have already prepare float parameters for receiving such data in your avatar), and you are ready to use. Alternatively, instead of modifying the JSON file, you can set an alias to the channel path, for example, you want your GPU temperature redirects to `gpu_temp` parameter of your avatar, you can add an entry to `channelAliases` section in the `config.yml` like this:

```yaml
channelAliases:
  /hardwares/*gpu/0/temperature/0: /avatar/parameters/gpu_temp
```

If you see theres some channels says "ignored" and you want to use those, or even you want some other types of hardware to be sent, you may need to edit `config.yml` besides the exe file. While editing, you can leave the program running and it will auto refreshes when you saves it.

Here are the available options in the config file:
- `ipAddress`: The IP address of your VRChat client, if it is the same machine, leave it `127.0.0.1`
- `port`: The OSC listening port of your VRChat client, default is `9000`, no need to change it unless you changed that for your client.
- `updateInterval`: The interval between updates in milliseconds, default is `1000`
- `ram`: Should RAM be monitored (`true`/`false`)
- `mainboard`: Should mother board be monitored (`true`/`false`)
- `cpu`: Should CPU be monitored (`true`/`false`)
- `gpu`: Should GPU be monitored (`true`/`false`)
- `hdd`: Should harddrives be monitored (`true`/`false`)
- `fanController`: Should fan controllers be monitored (`true`/`false`)
- `network`: Should network card be monitored (`true`/`false`)
- `patternConfigs`: You can adjusts the configuration by sent channel paths, it matches using glob (*).
    - `ignored`: The channel will not be sent if this is `true`
    - `min`: The minimum readings to be sent
    - `max`: The maximum readings to be sent, if `min` and `max` both are set, it will remaps the value to `0`-`1` (this is intentional for VRChat syncing parameters)
    - `stepped`: It will rounds the value to nearest integer before remaps it if this is `true`.
- `channelAliases`: Alternatively you can set an alias for specific channel output path, it also matches using glob (*).

If you see channel default prefixes with `/hardwares/`, it will be a hardware realtime info; if it is prefixes with `/datetime/`, it is just datetime info provided by system clock.

## License

[MIT](LICENSE)