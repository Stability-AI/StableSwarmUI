# Command Line Arguments

Most settings are configurable entirely via UI (or the `Settings.fds` file), however some settings intended for automated or dev usage are provided by command line instead. Additionally, some settings in the UI can be overridden by command line.

# Usage

An example for a personal launch configuration on a home Windows PC would be `.\launch-windows.ps1 --host * --port 7850 --environment development --launch_mode web`

Note that if your inputs are invalid, the program will refuse to start, with an error message indicating what value is wrong.

# Details

Argument | Default | Description
--- | --- | ---
`--data_dir` | `Data` | Override the default data directory.
`--settings_file` | `Data/Settings.fds` | If your settings file is anywhere other than the default, you must specify as a command line arg.
`--backends_file` | `Data/Backends.fds` | If your backends file is anywhere other than the default, you must specify as a command line arg.
`--environment` | `Production` | Can be `development` or `production` to set what ASP.NET Web Environment to use. `Development` gives detailed debug logs and errors, while `Production` is optimized for normal usage.
`--host` | `localhost` | Can be used to override the 'Network.Host' server setting.
`--port` | `7801` | Can be used to override the 'Network.Port' server setting.
`--asp_loglevel` | `warning` | Sets the minimum log level for ASP.NET web logger, as any of: `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`, `None`. Note 'information' here spams debug output.
`--loglevel` | `Info` | Minimum StableSwarmUI log level, as any of: `Debug`, `Info`, `Init`, `Warning`, `Error`, `None`. 'Info' here is the normal usage data.
`--user_id` | `local` | Set the local user's default UserID (for running in single-user mode, not useful in shared mode).
`--lock_settings` | `false` | If enabled, blocks in-UI editing of server settings by admins. Settings cannot be modified in this mode without editing the settings file and restarting the server.
`--ngrok-path` | (None) | If specified, will be used as the path to an `ngrok` executable, and will automatically load and configure ngrok when launching, to share your UI instance on a publicly accessible URL.
`--cloudflared-path` | (None) | If specified, will be used as the path to an `cloudflared` executable, and will automatically load and configure TryCloudflare when launching, to share your UI instance on a publicly accessible URL.
`--proxy-region` | (None) | If specified, sets the proxy (ngrok/cloudflared) region. If unspecified, defaults to closest.
`--ngrok-basic-auth` | (None) | If specified, sets an ngrok basic-auth requirement to access.
`--launch_mode` | `none` | Can be used to override the 'LaunchMode' server setting.
