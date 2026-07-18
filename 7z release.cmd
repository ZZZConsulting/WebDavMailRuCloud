set ver=1.26.07.18
set options=-tzip -mx9 -r -sse -x!*.pdb -x!*dev*
set dest=d:\temp\release

REM pushd BrowserAuthenticator\bin\Release\net7.0-windows
REM "C:\Program Files\7-Zip\7z.exe" a %options% "%dest%\BrowserAuthenticator-%ver%-net7.0-windows.zip" "*"
REM popd

pushd BrowserAuthenticator\bin\Release\net8.0-windows
"C:\Program Files\7-Zip\7z.exe" a %options% "%dest%\BrowserAuthenticator-%ver%-net8.0-windows.zip" "*"
popd

pushd BrowserAuthenticator\bin\Release\net9.0-windows
"C:\Program Files\7-Zip\7z.exe" a %options% "%dest%\BrowserAuthenticator-%ver%-net9.0-windows.zip" "*"
popd

REM pushd BrowserAuthenticator\bin\Release\net10.0-windows
REM "C:\Program Files\7-Zip\7z.exe" a %options% "%dest%\BrowserAuthenticator-%ver%-net10.0-windows.zip" "*"
REM popd

REM pushd WDMRC.Console\bin\Release\netcoreapp3.1
REM "C:\Program Files\7-Zip\7z.exe" a %options% "%dest%\WebDAVCloudMailRu-%ver%-dotNetCore3.1.zip" "*"
REM popd

REM pushd WDMRC.Console\bin\Release\net5.0
REM "C:\Program Files\7-Zip\7z.exe" a %options% "%dest%\WebDAVCloudMailRu-%ver%-dotNet5.zip" "*"
REM popd

REM pushd WDMRC.Console\bin\Release\net6.0
REM "C:\Program Files\7-Zip\7z.exe" a %options% "%dest%\WebDAVCloudMailRu-%ver%-dotNet6.zip" "*"
REM popd

REM pushd WDMRC.Console\bin\Release\net7.0
REM "C:\Program Files\7-Zip\7z.exe" a %options% "%dest%\WebDAVCloudMailRu-%ver%-dotNet7.zip" "*"
REM popd

REM pushd WDMRC.Console\bin\Release\net7.0-windows
REM "C:\Program Files\7-Zip\7z.exe" a %options% "%dest%\WebDAVCloudMailRu-%ver%-dotNet7Win.zip" "*"
REM popd

pushd WDMRC.Console\bin\Release\net8.0
"C:\Program Files\7-Zip\7z.exe" a %options% "%dest%\WebDAVCloudMailRu-%ver%-dotNet8.zip" "*"
popd

pushd WDMRC.Console\bin\Release\net8.0-windows
"C:\Program Files\7-Zip\7z.exe" a %options% "%dest%\WebDAVCloudMailRu-%ver%-dotNet8Win.zip" "*"
popd

pushd WDMRC.Console\bin\Release\net9.0
"C:\Program Files\7-Zip\7z.exe" a %options% "%dest%\WebDAVCloudMailRu-%ver%-dotNet9.zip" "*"
popd

pushd WDMRC.Console\bin\Release\net9.0-windows
"C:\Program Files\7-Zip\7z.exe" a %options% "%dest%\WebDAVCloudMailRu-%ver%-dotNet9Win.zip" "*"
popd

REM pushd WDMRC.Console\bin\Release\net10.0
REM "C:\Program Files\7-Zip\7z.exe" a %options% "%dest%\WebDAVCloudMailRu-%ver%-dotNet10.zip" "*"
REM popd

REM pushd WDMRC.Console\bin\Release\net10.0-windows
REM "C:\Program Files\7-Zip\7z.exe" a %options% "%dest%\WebDAVCloudMailRu-%ver%-dotNet10Win.zip" "*"
REM popd

pushd WDMRC.Console\bin\Release\net48
"C:\Program Files\7-Zip\7z.exe" a %options% "%dest%\WebDAVCloudMailRu-%ver%-dotNet48.zip" "*"
popd

