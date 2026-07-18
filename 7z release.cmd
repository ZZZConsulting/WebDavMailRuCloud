set ver=1.26.07.18
set options=-tzip -mx9 -r -sse -x!*.pdb -x!*dev*

REM "C:\Program Files\7-Zip\7z.exe" a %options% "BrowserAuthenticator-%ver%-net7.0-windows.zip" "BrowserAuthenticator\bin\Release\BrowserAuthenticator\bin\Release\net7.0-windows\*"
"C:\Program Files\7-Zip\7z.exe" a %options% "BrowserAuthenticator-%ver%-net8.0-windows.zip" "BrowserAuthenticator\bin\Release\net8.0-windows\*"
"C:\Program Files\7-Zip\7z.exe" a %options% "BrowserAuthenticator-%ver%-net9.0-windows.zip" "BrowserAuthenticator\bin\Release\net9.0-windows\*"
REM "C:\Program Files\7-Zip\7z.exe" a %options% "BrowserAuthenticator-%ver%-net10.0-windows.zip" "BrowserAuthenticator\bin\Release\net10.0-windows\*"

REM "C:\Program Files\7-Zip\7z.exe" a %options% "WebDAVCloudMailRu-%ver%-dotNetCore3.1.zip" "WDMRC.Console\bin\Release\WDMRC.Console\bin\Release\netcoreapp3.1\*"
REM "C:\Program Files\7-Zip\7z.exe" a %options% "WebDAVCloudMailRu-%ver%-dotNet5.zip" "WDMRC.Console\bin\Release\WDMRC.Console\bin\Release\net5.0\*"
REM "C:\Program Files\7-Zip\7z.exe" a %options% "WebDAVCloudMailRu-%ver%-dotNet6.zip" "WDMRC.Console\bin\Release\WDMRC.Console\bin\Release\net6.0\*"
REM "C:\Program Files\7-Zip\7z.exe" a %options% "WebDAVCloudMailRu-%ver%-dotNet7.zip" "WDMRC.Console\bin\Release\WDMRC.Console\bin\Release\net7.0\*"
REM "C:\Program Files\7-Zip\7z.exe" a %options% "WebDAVCloudMailRu-%ver%-dotNet7Win.zip" "WDMRC.Console\bin\Release\WDMRC.Console\bin\Release\net7.0-windows\*"
"C:\Program Files\7-Zip\7z.exe" a %options% "WebDAVCloudMailRu-%ver%-dotNet8.zip" "WDMRC.Console\bin\Release\net8.0\*"
"C:\Program Files\7-Zip\7z.exe" a %options% "WebDAVCloudMailRu-%ver%-dotNet8Win.zip" "WDMRC.Console\bin\Release\net8.0-windows\*"
"C:\Program Files\7-Zip\7z.exe" a %options% "WebDAVCloudMailRu-%ver%-dotNet9.zip" "WDMRC.Console\bin\Release\net9.0\*"
"C:\Program Files\7-Zip\7z.exe" a %options% "WebDAVCloudMailRu-%ver%-dotNet9Win.zip" "WDMRC.Console\bin\Release\net9.0-windows\*"
REM "C:\Program Files\7-Zip\7z.exe" a %options% "WebDAVCloudMailRu-%ver%-dotNet10.zip" "WDMRC.Console\bin\Release\net10.0\*"
REM "C:\Program Files\7-Zip\7z.exe" a %options% "WebDAVCloudMailRu-%ver%-dotNet10Win.zip" "WDMRC.Console\bin\Release\net10.0-windows\*"

"C:\Program Files\7-Zip\7z.exe" a %options% "WebDAVCloudMailRu-%ver%-dotNet48.zip" "WDMRC.Console\bin\Release\net48\*"
