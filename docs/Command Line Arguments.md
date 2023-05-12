# Command Line Arguments

All settings should be configurable entirely via UI in the future, however as a placeholder for now (and a second option for power users), some command line arguments are available.

# Usage

An example for a personal launch configuration on a home Windows PC would be `.\launch-windows.ps1 --host * --port 7850 --environment development`

Note that if your inputs are invalid, the program will refuse to start, with an error message indicating what value is wrong.

# Details

Argument | Default | Description
--- | --- | ---
`--settings_file` | `Data/Settings.fds` | If your settings file is anywhere other than the default, you must specify as a command line arg.
`--backends_file` | `Data/Backends.fds` | If your backends file is anywhere other than the default, you must specify as a command line arg.
`--environment` | `Production` | Can be `development` or `production` to set what ASP.NET Web Environment to use. `Development` gives detailed debug logs and errors, while `Production` is optimized for normal usage.
`--host` | `localhost` | Can be used to override the 'host' setting.
`--port` | `7801` | Can be used to override the 'port' setting.
`--asp_loglevel` | `warning` | Sets the minimum log level for ASP.NET web logger, as any of: `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`, `None`. Note 'information' here spams debug output.
`--loglevel` | `Info` | Minimum StableUI log level, as any of: `Debug`, `Info`, `Init`, `Warning`, `Error`, `None`. 'Info' here is the normal usage data.
`--user_id` | `local` | Set the local user's default UserID (for running in single-user mode, not useful in shared mode).
`--lock_settings` | `false` | If enabled, blocks in-UI editing of server settings by admins. Settings cannot be modified in this mode without editing the settings file and restarting the server.
`--ngrok-path` | (None) | If specified, will be used as the path to an `ngrok` executable, and will automatically load and configure ngrok when launching, to share your UI instance on a publicly accessible URL.
`--ngrok-region` | (None) | If specified, sets the ngrok region. If unspecified, defaults to closest.
`--ngrok-basic-auth` | (None) | If specified, sets an ngrok basic-auth requirement to access.
