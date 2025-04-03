REM This cleans all /obj/ dirs and hence removes stale nuget package references

CD %~dp0 

git clean -xdf **/obj/**

pause
