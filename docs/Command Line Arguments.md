# Command Line Arguments

All settings should be configurable entirely via UI in the future, however as a placeholder for now (and a second option for power users), some command line arguments are available.

# Usage

An example for a personal launch configuration on a home Windows PC would be `.\launch-windows.ps1 --host * --port 7850 --environment development`

Note that if your inputs are invalid, the program will refuse to start, with an error message indicating what value is wrong.

# Details

Argument | Default | Description
--- | --- | ---
`aspweb_mode` | `Production` | Can be `development` or `production` to set what ASP.NET Web Environment to use. `Development` gives detailed debug logs and errors, while `Production` is optimized for normal usage.
`host` | `localhost` | What web host address to use, `localhost` means your PC only, `*` means accessible to anyone that can connect to your PC (ie LAN users, or the public if your firewall is open). Advanced server users may wish to manually specify a host bind address here.
`port` | `7801` | What web port to use.
