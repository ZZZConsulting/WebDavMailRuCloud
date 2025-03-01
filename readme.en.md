# The **WebDAV Emulator** for RU-clouds: Cloud.Mail.Ru & Disk.Yandex.Ru

---

<a href="https://github.com/ZZZConsulting/WebDavMailRuCloud/releases/latest"><img src="https://img.shields.io/github/v/release/ZZZConsulting/WebDavMailRuCloud?include_prereleases"></a>
<img src="https://img.shields.io/github/last-commit/ZZZConsulting/WebDavMailRuCloud" target="_blank"> <img src="https://img.shields.io/github/downloads/ZZZConsulting/WebDavMailRuCloud/total" align="right" target="_blank">


This is the <a href="https://github.com/ZZZConsulting/WebDavMailRuCloud">ZZZ-fork</a> by <img src="https://avatars.githubusercontent.com/u/121279800?v=4&size=16" height="16pt" width="16pt"/> ZZZConsulting.

The root project by <img src="https://avatars.githubusercontent.com/u/5150160?s=48&v=4" height="15 pt" width="15 pt"/>YaR229
resides here: <a href="https://github.com/yar229/WebDavMailRuCloud">https://github.com/yar229/WebDavMailRuCloud/</a>

---

# Multilanguage README

[![en](https://img.shields.io/badge/lang-en-red.svg)](./readme.en.md)
[![ru](https://img.shields.io/badge/lang-ru-green.svg)](./readme.md)

---

## Important!

The English version of the README contains the most important steps to run the Emulator.
The rest things like **What**, **Why** and **When** may be found in [README in Russian](./readme.md).
In case you need that in English, please let me know. Or just use
[Google Translate](https://github-com.translate.goog/ZZZConsulting/WebDavMailRuCloud?_x_tr_sl=ru&_x_tr_tl=en&_x_tr_hl=ru&_x_tr_pto=wapp).

---

### Requirements <img src="https://habrastorage.org/files/72e/83b/159/72e83b159c2446b9adcdaa03b9bb5c55.png" width=200 align="right"/>
* [Windows](#windows) - [.NET Framework 4.8](https://dotnet.microsoft.com/en-us/download/dotnet-framework) / [.NET 8.0](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) / [.NET 9.0](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
* [Linux](#linux) - [.NET 8.0](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) / [.NET 9.0](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
* [OS X](#mac-os-x) - [.NET 8.0](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) / [.NET 9.0](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)

---

#### MRClient.exe
```
  -p, --port            (Default: 801) WebDAV emulator port or several ports separated by `,`.
  -h, --host            (Default: "http://127.0.0.1") WebDAV emulator host with protocol
                        (http://* for http://0.0.0.0).

  --proxy-address <socks|https|http>://<address>:<port>   Use proxy
  --proxy-user <username>                                 Proxy user name
  --proxy-password <password>                             Proxy password

  --maxthreads          (Default: 5) Maximum concurrent listening connections to the service.
  --maxconnections      (Default: 10) Maximum concurrent connections to cloud server per instance.
  --use-locks           (Default: false) Use locking feature.
  --cache-listing       (Default: 30) Timeout of in-memory cache of file and folder names
                        of the cloud in seconds. 0 disables the cache.
  --cache-listing-depth (Default: 1) Folder hierarchy depth when listing folders content.
                        Always equals 1 when cache-listing > 0.
                        To maximize performance
                        set cache-listing-depth=1 and cache-listing between 600 and 1800.
  --use-deduplicate     (Default: false) Enable deduplication (upload speedup, put by hash),
                        see Using deduplication section of the README.
  --disable-links       (Default: false) Disable support for shared folder and links
                        stored in item.links.wdmrc files.
  --detect-activity-interval
                        (Default: 15) Interval of time in seconds to track changes on Disk.Yandex
                        made without Emulator to reset in-memory cache,
                        0 disables the feature. Valid range is 4 - 60 seconds.

  --protocol            (Default: Autodetect) Cloud protocol
                        * Autodetect - see Auto-detect protocol section of the README
                        * WebM1Bin   - (Cloud.Mail.Ru) mix of mobile and DiskO protocols
                        * WebV2      - (Cloud.Mail.Ru) [deprecated] desktop browser protocol
                        * YadWeb     - (Disk.Yandex.Ru) desktop browser protocol,
                                       see Disk.Yandex.Ru section of the README

  --install <service name>          Install as Windows service (Windows .Net 4.8/7.0/8.0 versions only).
  --install-display <display name>  'Display name' of the service when installed as Windows service
                                    (Windows .Net 4.8/7.0/8.0 versions only).
  --uninstall <service name>        Uninstall Windows service (Windows .Net 4.8/7.0/8.0 versions only).

  --100-continue-timeout-sec  (Default: 1) Timeout in seconds,
                              to wait until the 100-Continue is received if chuck transfer.
  --response-timeout-sec      (Default: 100) Timeout in seconds,
                              to wait until 1-st byte from server is received.
  --read-write-timeout-sec    (Default: 300) Timeout in seconds,
                              the maximum duration of read or write operation.
                              If your Internet connection is slow
                              or you upload/download large files increase the value.
  --cloud-instance-timeout    (Default: 30) Cloud instance (server+login) expiration timeout in minutes.
                              On request the service creates one instance per cloud and login.
                              After specified period of time without requests the instance is recycled.

  --help                Display this help screen.
  --version             Display version information.

  -user-agent           Overrides default 'user-agent' header in requests to cloud servers.
  -sec-ch-ua            Overrides default 'sec-ch-ua' header in requests to cloud servers.
```

---

#### Hasher.exe

Calculating hashes for local files

```
  --files            (Group: sources) Filename(s)/wildcard(s) separated by space

  --lists            (Group: sources) Text files with wildcards/filenames separated by space

  --protocol         (Default: WebM1Bin) Cloud protocol to determine hasher

  -r, --recursive    (Default: false) Perform recursive directories scan

  --help             Display this help screen.

  --version          Display version information.
```

---

### Using deduplication (upload speedup, put by hash)

Edit `<Deduplicate>` section in `wdmrc.config`:

```
  <Deduplicate>
    <!-- Path for disk file cache -->
    <Disk Path = "d:\Temp\WDMRC_Cache" />

    <!--
      Cache: on disk or in-memory file caching
      Target:  path with filename in cloud, .NET regular expression,
               see https://docs.microsoft.com/ru-ru/dotnet/standard/base-types/regular-expressions
      MinSize: minimum file size
      MaxSize: maximum file size
      -->
    <Rules>
      <!-- cache any path/file contains "EUREKA" in disk cache-->
      <Rule Cache="Disk" Target = "EUREKA" MinSize = "0" MaxSize = "0" />

      <!-- small files less than 15000000 bytes will be cached in memory -->
      <Rule Cache="Memory" Target = "" MinSize = "0" MaxSize = "15000000" />

      <!-- files larger than 15000000 bytes will be cached on disk -->
      <Rule Cache="Disk" Target = "" MinSize = "15000000" MaxSize = "0" />
    </Rules>
  </Deduplicate>
```
Then run with `--use-deduplicate` command line key.

---

### Cloud protocol and `Autodetect`

Direct use of WebDAV with Cloud.Mail.Ru or Disk.Yandex.Ru is full of problems.
The WebDAV emulator is made to solve the problems using unofficial APIs.
We call the subset of API methods used to achieve a desired result a `protocol`.
When user going to reach a cloud through WebDAV emulator,
the WebDAV emulator must choose:
* what cloud should be used (Cloud.Mail.Ru or Disk.Yandex.ru),
* what protocol (API) must be used,
* what kind of authentication must be used (basic login+password or browser).

***Autodetect***. How it works

Step **1**. The Cloud

If user specified `login` in email style (e.g. John@yandex.ru or John@mail.ru)
the Cloud is determined by email domain.
Otherwise (e.g. `login` is John) the Cloud is determined by `protocol` parameter of the application.

Step **2**. The Protocol

In case when the Cloud is determined and the Protocol is not,
the WebDAV emulator uses
* WebM1Bin protocol for Cloud.Mail.Ru and
* YadWeb protocol for Disk.Yandex.Ru.

Step **3**. The Authentication type

The WebM1Bin protocol has only one type of authentication - `login` and `password`.

The YadWeb protocol for Disk.Yandex.Ru has two types of authentication:

* `login` and `password`
and
* browser authentication using `BrowserAuthenticator` application.

By default the login+password authentication is used.

If `BrowserAuthenticator` is configured in `wdmrc.config` and
user `password` equals to `BrowserAuthenticator` password
the browser authentication is used.

User can give the WebDAV emulator the suggestion which type of authentication to use by appending symbol `!` or `?` in `login` string at leftmost position.
`?` means the browser authentication must be used.
`!` means the browser authentication must **not** be used.

The leftmost symbols `!` and `?` are removed from login while talking to clouds.

If browser authentication is not used, a user have to fill `password` with password of cloud account.
The WebDAV emulator with YadWeb emulates browser, so the main password of the account must be used to connect to the cloud!
Application password generated in Yandex account will fail!

If browser authentication is used, a user have two options.
In both cases `BrowserAuthenticator` application is going to authenticate incoming request by matching incoming password against password on Settings window.
The first options is to pass to WebDAV emulator exact value of password on Settings window of the `BrowserAuthenticator` application.
The second option is to pass empty password to WebDAW emulator. That means WebDAW emulator should take the password from password attribute of the `BrowserAuthenticator` tag in `wdmrc.config`.

If you change password in Settings window of `BrowserAuthenticator` application quite often,
you may want to setup password once in `wdmrc.config` and use login with empty password.

If you believe some one except you can try to connect to you `BrowserAuthenticator` application,you can put wrong password into password attribute of the `BrowserAuthenticator` tag in `wdmrc.config`
and use login and correct password (equal to password in Settings window of `BrowserAuthenticator` application) while connecting to WebDAW emulator.

---

### Disk.Yandex.Ru

Issues of WebDAV by Disk.Yandex.Ru

* It seems like WebDAV of Disk.Yandex.Ru is limited by speed since 2019.
* After file uploading Yandex servers calculating hash.
  E.g. for a 10GB file it may take ~1..2 minutes depending on server load.
  So most of WebDAV clients drops connection on timeout.
* There's no WebDAV info in official help now. WTF?
* Since 2019 Yandex states that WebDAV is OK for all supported applications made by Yandex,
  also Yandex does not support and does not guarantee correct work
  of any third party application with their WebDAV.

To bypass the limit issue the WebDAV emulator uses the unofficial Disk.Yandex.Ru Web API.
The salvation have 2 steps:

**1**) Disk.Yandex.Ru WebDAV authentication

To get in to Disk.Yandex.Ru you have to be authenticated by Yandex by any of two ways:

* By `login` & `password` only.
  The account must be configured for log in using login and password only.
  Yandex account security have an option to create Application passwords.
  Do not use Application password when you connecting to this WebDAW service!
  You **must** use the main account password only
  because this service emulates you on the web using browser.
  Fill the `login` field with email (e.g. John@yandex.ru instead of John).

* By standard web site authentication using specially designed `browser` called `BrowserAuthenticator`.
  The BrowserAuthenticator application is designed to be run at Windows startup.
  It hides in system tray and waits for incoming authentication
  request from the WebDAV emulator.
  When requested, BrowserAuthenticator shows a browser window allowing you to log into cloud
  using `login` and `password` or `login` and `password` and SMS or even QR code.
  When you successfully logged in, the program takes all data from the browser
  and sends it back to the WebDAV emulator, then the service talks
  to cloud servers using you authentication information.
  Because your authentication information is used, you **must** keep cache with the information **secured**!
  For more information read the `BrowserAuthenticator` section.

**2**) File operations with Disk.Yandex.Ru

Once authenticated all file operations such as reading, writing, creating, deleting,
and so on are available for use.
Because the WebDAV emulator emulates you using a browser the Issue is not applied.
Unfortunately, there is no guarantee the service is going to work infinitely long.
Time to time Yandex makes unexpected changes in their programs.

---

### BrowserAuthenticator

is a specially designed `browser` meant be run at Windows startup.
It hides in system tray and waits for incoming authentication
request from the WebDAV emulator.
When requested, BrowserAuthenticator shows a browser window allowing you to log into cloud
using `login` and `password` only
or `login` and `password` and SMS code
or even QR code.
When you successfully logged in, the program takes all data from the browser
and sends it back to the WebDAV emulator, then the service talks
to cloud servers using you authentication information.

**Remember!**

The WebDAV emulator impersonates you using you credentials and security cookies taken from the `BrowserAuthenticator` application when you logged in!
Both the WebDAV emulator and the `BrowserAuthenticator` application store the security information on drives.
**Read the following instruction very carefully!**

***Step-by-step setup of BrowserAuthenticator***

1. You need to choose the location for the `BrowserAuthenticator`.
When `BrowserAuthenticator` runs it creates subfolders below folder where it is placed. It means it should have enough rights to create and delete folders and files.
One of subfolders is going to have security information enough to connect to your cloud without your interaction, so the place **must** be secured!

One of the best options for hosting `BrowserAuthenticator` is
`%userprofile%\AppData\Local\Applications\BrowserAuthenticator`
(you need to create the folders yourself in `%userprofile%\AppData\Local`)
or any other folder in `%userprofile%\AppData\Local` of your choice.

2. Download BrowserAuthenticator-*-windows.zip package and extract it's content to the `BrowserAuthenticator` folder.

3. Press Win+R and run `shell:startup`. Put shortcut of the `BrowserAuthenticator` program in startup folder,
so the `BrowserAuthenticator` will be started everytime you log in to Windows.
Don't make copy of the `BrowserAuthenticator` in startup folder, the shortcut only!
Start the `BrowserAuthenticator` manually for the first time.

4. Since it started the `BrowserAuthenticator` stays in system tray until you system reboot or you manually exit it using menu on tray icon.
Use mouse double click on the `BrowserAuthenticator` tray icon
<img src="BrowserAuthenticator/files/cloud.ico"/> to open up Settings window of the application.

5. On the Setting Window setup then `port` and the `password` for incoming connections.
Type in an email and press Test to check the browser is functional.
For the `password` you can use any text string. To get things easy you can generate password by clicking the blue text under the field.

6. Go to WebDAV emulator application folder. Open the `wdmrc.config` and edit the `<BrowserAuthenticator>` tag (add the tag if it's missing).

Attributes of `<BrowserAuthenticator>`:

* `Url`="http://`localhost`:`<port>`/" - type in the address of the PC running BrowserAuthenticator application,
  `port` is the `port` you set in Settings window on the previous step.
  `localhost` could be replaced by any wold wide IP address reachable by WebDAV emulator.

* `Password` is the text string you set in Settings window on the previous step.
  You should keep the `Password` in secret. Otherwise someone could connect to you `BrowserAuthenticator` application and ***steal*** you browser cookies, you credentials and you cloud data!

* `CacheDir` is the full path to a folder where WebDAW emulator is going to keep information received from `BrowserAuthenticator` application.
  It contains browser cookies, the information enough to connect to your cloud impersonating you!
  Keep the folder secured as much as possible!
  
On step 1, you have created a folder for `BrowserAuthenticator` application somewhere in `%userprofile%\AppData\Local`
Make another subfolder in `BrowserAuthenticator` application folder and put it's path into `CacheDir` attribute, so this way you have to keep in secret only one location not two.

In cage you don't want to cache information received from `BrowserAuthenticator` application remove the `CacheDir` attribute or put an empty string as a value. 

---

#### Using as Windows service

Using as Windows service (**dotNet48 package only**).
* Run `cmd` with Administrator rights
* Then, for example, `wdmrc.exe --install wdmrc -p 801 --maxthreads 15` <br/>
* `net start wdmrc`

Using as Windows service (**dotNet7Win/dotNet8Win packages only**).

* For install: Run `cmd` with Administrator rights,
  change parameters and values, then run
  `wdmrc.exe --install WebDavService --maxthreads 10 --maxconnections 20 --port 801 --cache-listing 180`

* For uninstall: Run `cmd` with Administrator rights, type in and run
  `wdmrc.exe --uninstall WebDavService`

---

### Features

***How to use encryption***

Using XTS AES-256 on-the-fly encryption/decryption

* Set (en/de)cryption password
  * with `>>crypt passwd` special command <br/>
    or
  * Add `#` and separator string to your login: `login@mail.ru#_SEP_`
  * After your mail.ru password add separator string and password for encrypting: `MyLoginPassword_SEP_MyCryptingPassword`

* Mark folder as encrypted using `>>crypt init` command
* After that files uploaded to this folder will be encrypted

***Commands*** <br/>
Commands executed by making directory with special name.<br/>
Parameters with spaces must be screened by quotes.
* `>>join SHARED_FOLDER_LINK` Clone shared cloud.mail.ru file/folder to your account
* `>>join #filehash filesize [/][path]filename` Clone cloud.mail.ru file to your account by known hash and size
* `>>link SHARED_FOLDER_LINK [linkname]` Link shared folder without wasting your space (or manually edit file /item.links.wdmrc)
* `>>link check` Remove all dead links (may take time if there's a lot of links)
* `>>move` `/full/path/from /full/path/to` Fast move (if your client moves inner items recursively)
* `>>copy` `/full/path/from /full/path/to` Fast copy (if your client copies inner items recursively)
* `>>lcopy` `x:/local/path/from /full/server/path/to` If file already in cloud, add it by hash without uploading
* `>>rlist` [[/]path] [list_filename]	list [path] to [list_filename]
* `>>del [[/]path]` Fast delete (if your client makes recursive deletions of inner items)
* `>>share [[/]path]` Make file/folder public <br/>
  - and create `.share.wdmrc` file with links
* `>>sharev [[/]path] [resolution]` Make media file public <br/>
  - `resolution` = `0p` (all), `240p`, `360p`, `480p`, `720p`, `1080p`
  - and create `.share.wdmrc` file with public and direct play links
* `>>pl [[/]path]  [resolution]` Make media file public <br/>
  - `resolution` = `0p` (all), `240p`, `360p`, `480p`, `720p`, `1080p`
  - and create `.share.wdmrc` file with public and direct play links <br/>
  - and create `.m3u8` playlist file
* `>>crypt init` Mark current folder as encrypted
* `>>crypt passwd password_for_encryption_decryption` Set password for encryption/decryption

***Settings*** in `wdmrc.exe.config`
* Logging <br/>
    `<config><log4net>` <br/>
    It's standard [Apache log4net](https://logging.apache.org/log4net/) configurations, take a look for [examples](https://logging.apache.org/log4net/release/config-examples.html)
    Additionally you can use `protocol` and `port` properties taken from command-line parameters.
* Default video resolution for generated m3u playlists
    `<config><DefaultSharedVideoResolution>` <br/>
    Values:
      `0p`      auto, m3u contains links to all available resolutions 
      `240p`    ~ 352 x 240
      `360p`    ~ 480 x 360
      `480p`    ~ 858 x 480
      `720p`    ~ 1280 x 720
      `1080p`   ~ 1920 x 1080
* Default User-Agent <br/>
    `<config><DefaultUserAgent>` <br/>
    Default user-agent for web requests to cloud.
* Special command prefix <br/>
    `<config><AdditionalSpecialCommandPrefix>` <br/>
    custom special command prefix instead of `>>`. Make possible to use special commands if client doesn't allow `>>`.
* Enable/disable WebDAV properties <br/>
    `<config><WebDAVProps>` <br/>
    set `false` on properties you don't need to speedup listing on large catalogs / slow connections.
* 2 Factor Authentication <br/>
    At this time you can use
    * `<TwoFactorAuthHandler Name = "AuthCodeConsole"/>` - asks for authcode in application console
    * `<TwoFactorAuthHandler Name = "AuthCodeWindow"/>` - asks for authcode in GUI window (only for .NET Framework releases)
    * 
        ```
        <TwoFactorAuthHandler Name = "AuthCodeFile">
            <Param Name = "Directory" Value = "d:"/>
            <Param Name = "FilenamePrefix" Value = "wdmrc_2FA_"/>
        </TwoFactorAuthHandler>
        ```
       user must write authcode to file. For example, user `test@mail.ru` writes code to `d:\wdmrc_2FA_test@mail.ru`.
    
    
    Be careful, this methods does not usable when application started as a service/daemon. <br>
    You can make your own 2FA handlers inherited from `ITwoFaHandler` and put it in separate dll which name starts with `MailRuCloudApi.TwoFA`
    
Connect with (almost any) file manager that supports WebDAV using Basic authentication with no encryption and
* your cloud.mail.ru email and password
* or `anonymous` login if only public links list/download required ([WinSCP script example](https://github.com/yar229/WebDavMailRuCloud/issues/146#issuecomment-448978833))

Automatically split/join when uploading/downloading files larger than cloud allows.

[Russian FAQ](https://gist.github.com/yar229/4b702af114503546be1fe221bb098f27) <br/>
[geektimes.ru - Снова про WebDAV и Облако Mail.Ru](https://geektimes.ru/post/285520/) <br/>
[glashkoff.com - Как бесплатно подключить Облако Mail.Ru через WebDAV](https://glashkoff.com/blog/manual/webdav-cloudmailru/) <br/>
[manjaro.ru - Облако Mail.Ru подключаем через эмулятор WebDAV как сетевой диск](https://manjaro.ru/how-to/oblako-mailru-podklyuchaem-cherez-emulyator-webdav-kak-setevoy-disk.html) <br/>


<br/>

<details> 
<summary>Using from Windows Explorer requires enabled Basic Auth for WebDAV</summary>
* Press Win+R, type `regedit`, click OK
* HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\WebClient\Parameters
* Right click on the BasicAuthLevel and click Modify
* In the Value data box, type 2, and then click OK.
* Reset computer (or run `cmd` with admin rights and then `net stop webclient`, `net start webclient`)
</details>

<details> 
<summary>Use as Windows disk</summary>
```
net use ^disk^: http://^address^:^port^ ^your_mailru_password^ /USER:^your_mailru_email^
```
</details>

<details>
<summary>Faster WebDAV Performance in Windows 7</summary>
Windows 7 client might perform very bad when connecting to any WebDAV server. This is caused, because it tries to auto-detect any proxy server before any request. Refer to KB2445570 for more information.

* In Internet Explorer, open the Tools menu, then click Internet Options.
* Select the Connections tab.
* Click the LAN Settings button.
* Uncheck the “Automatically detect settings” box.
* Click OK until you’re out of dialog.
</details>

<details>
<summary>By default, Windows limits file size to 5000000 bytes, you can increase it up to 4Gb</summary>
* Press Win+R, type `regedit`, click OK
* HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\WebClient\Parameters
* Right click on the FileSizeLimitInBytes and click Modify
* Click on Decimal
* In the Value data box, type 4294967295, and then click OK.
* Reset computer (or run `cmd` with admin rights and then `net stop webclient`, `net start webclient`)
</details>

<details>
<summary>Wrong disk size when mapped as Windows drive</summary>
[Microsoft says - "It's not a bug, it's by design"](https://support.microsoft.com/en-us/kb/2386902)
</details>


#### Linux

(tested under [Elementary OS](https://elementary.io) and [Lubuntu](http://lubuntu.net/))
* download and unzip [latest](https://github.com/ZZZConsulting/WebDavMailRuCloud/releases/latest) release  <sub><sup>([obsolete alternative way](https://toster.ru/q/375448) from [Алексей Немиро](https://toster.ru/user/AlekseyNemiro) )</sup></sub>
* .NET Framework (WebDAVCloudMailRu-*-dotNet48.zip)
  * `sudo apt install apt mono-complete`
  * `mono wdmrc.exe -p <port>`
* .NET  (`WebDAVCloudMailRu-*-dotNet*.zip` but not a `WebDAVCloudMailRu-*-dotNet*Win.zip` version)
  * install [.NET](https://dotnet.microsoft.com/en-us/download#linuxredhat)
  * `dotnet wdmrc.dll <params>`


See also 
* [Package for Gentoo Linux](https://github.com/yar229/WebDavMailRuCloud/issues/66) by [powerman](https://github.com/powerman)
* Docker image by [slothds](https://github.com/slothds) ([DockerHub](https://hub.docker.com/r/slothds/wdmrc-proxy/), [GitHub](https://github.com/slothds/wdmrc-proxy))
* Docker image by [ivang7](https://github.com/ivang7) HTTP & HTTPS [DockerHub](https://hub.docker.com/r/ivang7/webdav-mailru-cloud)




Mount with davfs2
* `mkdir /mnt/<folder>`
* edit `/etc/davfs2/davfs2.conf` set `use_locks       0`
* `sudo mount --rw -t davfs http://<address>:<port> /mnt/<folder>/ -o uid=<current_linux_user>`

As a service (daemon)
* https://github.com/yar229/WebDavMailRuCloud/issues/214


CERTIFICATE_VERIFY_FAILED exception
[Issue 56](https://github.com/yar229/WebDavMailRuCloud/issues/56)
[default installation of Mono doesn’t trust anyone](http://www.mono-project.com/docs/faq/security/)

In short:
```
# cat /etc/ssl/certs/* >ca-bundle.crt
# cert-sync ca-bundle.crt
# rm ca-bundle.crt
```

#### Mac OS X

* download and unzip [latest](https://github.com/yar229/WebDavMailRuCloud/releases/latest) release  <sub><sup>([obsolete alternative way](https://toster.ru/q/375448) from [Алексей Немиро](https://toster.ru/user/AlekseyNemiro) )</sup></sub>
* .Net Framework (WebDAVCloudMailRu-*-dotNet45.zip)
  * `brew install mono` (how to install [brew](https://brew.sh/))
  * `mono wdmrc.exe -p <port>`
* .Net Core (WebDAVCloudMailRu-*-dotNetCore20.zip)
  * install [.NET Core](https://www.microsoft.com/net/core#macos)
  * `dotnet wdmrc.dll <params>`

Use any client supports webdav.


#### Remarks
* [**RaiDrive**](https://www.raidrive.com/)
* [**NetDrive**](http://www.netdrive.net/)
* [**rclone mount**](https://rclone.org/)
* [**Total Commander**](http://www.ghisler.com/): 
  - requires to update `WebDAV plugin` to [v.2.9](http://ghisler.fileburst.com/fsplugins/webdav.zip)
  - turn on `(connection properties) -> Send\Receive accents in URLs as UTF-8 Unicode`
* [**WebDrive**](https://southrivertech.com/products/webdrive/): 
  - disable `(disk properties) -> HTTP Settings -> Do chunked upload for large files.`
* [**CarotDAV**](http://rei.to/carotdav_en.html): 
  - check `(connection properties) -> Advanced -> Don't update property.`
* avoid using Unicode non-printing characters such as [right-to-left mark](https://en.wikipedia.org/wiki/Right-to-left_mark) in file/folder names


#### Big thanks
* [Ramon de Klein](https://github.com/ramondeklein) for [nwebdav server](https://github.com/ramondeklein/nwebdav)
* [Erast Korolev](https://github.com/erastmorgan) for [Mail.Ru.net-cloud-client](https://github.com/erastmorgan/Mail.Ru-.net-cloud-client)
* [Gareth Lennox](https://bitbucket.org/garethl/) for [XTSSharp](https://bitbucket.org/garethl/xtssharp)
* [C-A-T](https://github.com/C-A-T9LIFE) for testing and essential information
* [YaR229](https://github.com/yar229) for original [WebDavMailRuCloud](https://github.com/yar229/WebDavMailRuCloud)


#### See also<br>
*  Official client [Disk-O:](https://disk-o.cloud/)
*  [Total Commander plugin for cloud.mail.ru service](https://github.com/pozitronik/CloudMailRu)<br>
*  [MARC-FS - FUSE filesystem attempt for Mail.Ru Cloud](https://gitlab.com/Kanedias/MARC-FS)<br>
