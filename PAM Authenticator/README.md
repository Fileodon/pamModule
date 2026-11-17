### Steps
* `sudo apt install dotnet-runtime-10.0 dotnet-host-10.0 dotnet-hostfxr-10.0 dotnet-sdk-10.0`
* `cd PAM\ Authenticator/`
* `dotnet publish -r linux-x64 --self-contained -p:PublishSingleFile=true`
* `sudo mkdir /opt/pam-auth`
* `sudo cp bin/Release/net10.0/linux-x64/publish/PAM\ Authenticator /opt/pam-auth/PAM_Authenticator`
* `sudo chmod 755 /opt/pam-auth/PAM_Authenticator`
* `sudo chmod +x /opt/pam-auth/PAM_Authenticator`
* `sudo ./setup-pam-auth.sh`
* `sudo systemctl reload ssh`

During installation, `setup-pam-auth.sh` downloads the current public key and
installs it as `public.pem` beside `PAM_Authenticator`. Set
`PAM_PUBLIC_KEY_URL` to use a different API endpoint, or set
`PAM_PUBLIC_KEY_PATH` to choose another destination:

```bash
sudo PAM_PUBLIC_KEY_URL="https://example.com/pam/publickey" \
  PAM_PUBLIC_KEY_PATH="/opt/pam-auth/public.pem" \
  ./setup-pam-auth.sh
```

If the endpoint requires authentication, set `PAM_PUBLIC_KEY_AUTH_HEADER`,
for example:

```bash
sudo PAM_PUBLIC_KEY_AUTH_HEADER="Authorization: Bearer <token>" ./setup-pam-auth.sh
```

Logs are written to `logs/pam-authenticator-YYYYMMDD.log` relative to the
published application directory. Set `PAM_LOG_PATH` to override the log file
path. Set `PAM_PUBLIC_KEY_PATH` to override the default `public.pem` path.