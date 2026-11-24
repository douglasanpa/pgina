# pGina

pGina is a pluggable Open Source GINA and CredentialProvider replacement.
Plugins are written in managed code and allow for user authentication, session
management and login time actions.

## Building

Requires Visual Studio 2026 or 2022 (or the Build Tools) with the .NET desktop
and C++ desktop workloads. From the repository root:

    powershell -ExecutionPolicy Bypass -File tools\build.ps1

pGina is built to `pGina\src\bin` and the plugins to `Plugins\bin`. To also
build the installer (needs [Inno Setup 6](https://jrsoftware.org/isinfo.php)):

    powershell -ExecutionPolicy Bypass -File Installer\make.ps1

Every push and pull request is also built by GitHub Actions, which keeps the
binaries and the installer as downloadable artifacts of the run.

## Releasing

Push a tag named after the version, e.g. `v3.10.0.0`. The workflow builds the
installer with that version and publishes it as a GitHub release. Keep the
version in `pGina/src/Core/Properties/SharedAssemblyInfo.cs` and
`pGina/src/CredentialProvider/CredentialProvider.rc` in step with the tag.

The release carries `pGinaSetup-<version>.exe` and its `.sha256`: installed
copies use both to update themselves (below).

## Updates

pGina looks for new releases on its GitHub page (`/releases/latest`, not the
API, so a whole lab behind one address isn't rate limited). Settings under
`HKLM\SOFTWARE\pGina3.fork`, also in Configuration > General > Updates:

| Value | Default | Meaning |
|---|---|---|
| `UpdateCheck` | `True` | the service checks every 6 hours (plus up to one hour at random) and the configuration window shows when a new version is available, with a button to download and install it |
| `AutoUpdate` | `False` | the service also downloads the new version and installs it while nobody is logged on; the computer restarts at the end |
| `UpdateSource` | this repository | another repository or mirror with the same release layout |

A download is kept only if its SHA-256 matches the `.sha256` of the release
and its file version is the release version. It is stored in
`C:\ProgramData\pGina.fork\Update`, which only SYSTEM and the administrators
can write to. Installing stops the pGina service, and a session open at that
moment could no longer be unlocked, so automatic installs wait until nobody is
logged on; each version is tried at most once a day. The service log
(`log\pGina.Service.ServiceHost_log.txt`) records every check and install.

Machines whose version is managed elsewhere (Salt, GPO, SCCM) should set both
values to `False`.

Translations: see [tools/i18n](tools/i18n/README.md).

## License

Copyright (c) 2016, pGina Team

All Rights Reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

 * Redistributions of source code must retain the above copyright notice, this 
   list of conditions and the following disclaimer.
 * Redistributions in binary form must reproduce the above copyright notice, 
   this list of conditions and the following disclaimer in the documentation 
   and/or other materials provided with the distribution.
 * Neither the name of the pGina Team nor the names of its contributors may 
   be used to endorse or promote products derived from this software without 
   specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
