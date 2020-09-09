@echo off

SET APP_NAME=PBITUtility.exe
SET ILMERGE_BUILD=Release
SET ILMERGE_VERSION=3.0.41
SET ILMERGE_PATH=..\packages\ILMerge.%ILMERGE_VERSION%\tools\net452

echo Merging %APP_NAME% ...

"%ILMERGE_PATH%"\ILMerge.exe bin\%ILMERGE_BUILD%\%APP_NAME%  ^
  /out:..\%APP_NAME% ^
  bin\%ILMERGE_BUILD%\Microsoft.Bcl.AsyncInterfaces.dll ^
  bin\%ILMERGE_BUILD%\System.Buffers.dll ^
  bin\%ILMERGE_BUILD%\System.Memory.dll ^
  bin\%ILMERGE_BUILD%\System.Numerics.Vectors.dll ^
  bin\%ILMERGE_BUILD%\System.Runtime.CompilerServices.Unsafe.dll ^
  bin\%ILMERGE_BUILD%\System.Text.Encodings.Web.dll ^
  bin\%ILMERGE_BUILD%\System.Text.Json.dll ^
  bin\%ILMERGE_BUILD%\System.Threading.Tasks.Extensions.dll ^
  /ndebug


dir ..\%APP_NAME%