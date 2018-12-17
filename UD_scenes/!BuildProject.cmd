rem %CD% is the current directory

set TRG=%BERTECBINOUT%\Plugins\Visualizations\CastleScene.exe

rem This builds the exe and the related files directly into the Assessments folder (TRG)

"c:\Program Files\Unity\Editor\Unity.exe" -nographics -batchmode -quit -projectPath "%CD%" -buildWindowsPlayer %TRG%

pause
