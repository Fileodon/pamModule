# Installationsvejledning: PAM Authenticator

Denne vejledning beskriver, hvordan `PAM Authenticator` installeres på en
Linux-server, så SSH-login for én bruger kan godkendes med en JWT via PAM.

> **Vigtigt:** Behold en eksisterende SSH-session åben, mens installationen
> testes. En fejl i PAM- eller SSH-konfigurationen kan ellers låse dig ude af
> serveren.

## Forudsætninger

- En Debian- eller Ubuntu-baseret Linux-server.
- Root-adgang via `sudo`.
- En installeret og fungerende OpenSSH-server.
- `curl` eller `wget` til download af den offentlige nøgle.
- .NET 10 SDK til bygning af programmet.
- En bruger, som skal bruge JWT-login. Standardbrugeren er `fileodon`.
- Et API-endpoint, der returnerer PAM Authenticator's offentlige nøgle i PEM-format.

Kontrollér eventuelt SSH-serveren og installer de nødvendige værktøjer:

```bash
sudo apt update
sudo apt install openssh-server curl
sudo systemctl enable --now ssh
```

Installer .NET 10 SDK, hvis det ikke allerede er installeret:

```bash
sudo apt install dotnet-sdk-10.0
```

## 1. Byg en Linux-version

Klon eller kopiér projektet til serveren, og gå til projektmappen:

```bash
cd "PAM Authenticator"
```

Byg en selvstændig Linux x64-udgivelse. Den selvstændige udgivelse kræver ikke,
at .NET-runtime installeres på målserveren:

```bash
dotnet publish -c Release -r linux-x64 --self-contained \
  -p:PublishSingleFile=true
```

Den færdige fil placeres i:

```text
bin/Release/net10.0/linux-x64/publish/PAM Authenticator
```

## 2. Installér programmet

Opret installationsmappen, kopiér programmet, og gør det eksekverbart:

```bash
sudo mkdir -p /opt/pam-auth
sudo cp "bin/Release/net10.0/linux-x64/publish/PAM Authenticator" \
  /opt/pam-auth/PAM_Authenticator
sudo chmod 755 /opt/pam-auth/PAM_Authenticator
```

Kopiér eller behold `setup-pam-auth.sh` i projektmappen. Kør de følgende
kommandoer fra den mappe, hvor scriptet ligger:

```bash
sudo chmod +x setup-pam-auth.sh
```

## 3. Kør installationsscriptet

Standardinstallationen bruger:

- Bruger: `fileodon`
- Program: `/opt/pam-auth/PAM_Authenticator`
- PAM-konfiguration: `/etc/pam.d/sshd`
- SSH-konfiguration: `/etc/ssh/sshd_config`
- Nøgle-endpoint:
  `https://api.dev.fileodon.dev/api/organization/sftpconnection/pam/publickey`

Installér integrationen og genindlæs SSH:

```bash
sudo ./setup-pam-auth.sh --reload
```

Scriptet downloader den offentlige nøgle til
`/opt/pam-auth/public.pem`, opretter sikkerhedskopier af PAM- og SSH-filerne
og validerer SSH-konfigurationen med `sshd -t`.

### Anden bruger

Angiv den bruger, der skal beskyttes, med `TARGET_USER`:

```bash
sudo TARGET_USER="min-bruger" ./setup-pam-auth.sh --reload
```

### Brugerdefineret nøgle-endpoint

Hvis nøglen hentes fra et andet endpoint:

```bash
sudo PAM_PUBLIC_KEY_URL="https://eksempel.dk/pam/publickey" \
  ./setup-pam-auth.sh --reload
```

Hvis endpointet kræver en HTTP-header:

```bash
sudo PAM_PUBLIC_KEY_AUTH_HEADER="Authorization: Bearer <token>" \
  ./setup-pam-auth.sh --reload
```

Undgå at gemme tokens i versionsstyringen eller i shell-historikken.

En anden placering til den offentlige nøgle kan angives med
`PAM_PUBLIC_KEY_PATH`. Den samme sti skal være tilgængelig for programmet, når
PAM starter det:

```bash
sudo PAM_PUBLIC_KEY_PATH="/opt/pam-auth/public.pem" \
  ./setup-pam-auth.sh --reload
```

## 4. Kontrollér installationen

Kontrollér først SSH-konfigurationen manuelt:

```bash
sudo sshd -t
```

Kontrollér, at PAM er aktiveret:

```bash
sudo sshd -T | grep -Ei '^(usepam|kbdinteractiveauthentication|authenticationmethods) '
```

For den valgte bruger skal outputtet vise, at `UsePAM` er aktiv, og at
`keyboard-interactive` bruges som godkendelsesmetode.

Test derefter et nyt SSH-login fra en separat terminal. Luk ikke den
eksisterende administrative session, før login er bekræftet.

Programlogfiler skrives som standard til:

```text
/opt/pam-auth/logs/pam-authenticator-YYYYMMDD.log
```

Logplaceringen kan ændres med miljøvariablen `PAM_LOG_PATH`.
JWT-audience er som standard serverens maskinnavn og kan ændres med
`PAM_AUDIENCE`.

## Fjern installationen eller rull tilbage

Fjern de blokke, som scriptet har tilføjet, og genindlæs SSH:

```bash
sudo ./setup-pam-auth.sh --remove --reload
```

Scriptet opretter tidsstemplede sikkerhedskopier af
`/etc/pam.d/sshd` og `/etc/ssh/sshd_config` før ændringerne foretages.
Hvis `sshd -t` afviser den nye konfiguration, gendannes begge filer
automatisk. Ved manuel gendannelse skal du kontrollere filerne grundigt og
køre `sudo sshd -t`, før SSH genindlæses.

## Fejlfinding

- **`run as root (sudo)`**: Kør scriptet med `sudo`.
- **`curl or wget is required`**: Installér `curl` eller `wget`.
- **Kunne ikke hente den offentlige nøgle**: Kontrollér endpoint, netværk og
  eventuel `PAM_PUBLIC_KEY_AUTH_HEADER`.
- **`sshd rejected the new config`**: Se den viste fejl, og brug de oprettede
  `.bak.<tidsstempel>`-filer til sammenligning.
- **Programmet kan ikke finde `public.pem`**: Kontrollér
  `PAM_PUBLIC_KEY_PATH`, filens rettigheder og at filen indeholder en PEM
  public key.
- **Login fejler uden tydelig forklaring**: Se PAM Authenticator-loggen og
  systemloggen, for eksempel med `sudo journalctl -u ssh`.
