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