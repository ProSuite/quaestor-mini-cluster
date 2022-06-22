@cd /d "%~dp0"

@echo off

echo.removing contents of dist folder...
cd src
del /S /Q dist
cd dist
rmdir /S /Q docs
cd ../..

echo.Building Documentation...
cd docs
call make html
cd ..

echo.Moving Documentation to dist...
XCOPY /Y /E /I docs\build\html src\dist\docs

cd src
echo.Creating distribution package...
python setup.py sdist
pause
