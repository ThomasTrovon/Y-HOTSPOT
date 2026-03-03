dotnet clean .\HotspotApp\HotspotApp.csproj

Remove-Item -Path .\HotspotApp\bin -Force -Recurse
Remove-Item -Path .\HotspotApp\obj -Force -Recurse
Remove-Item -Path .\Publish -Force -Recurse

Write-Host "Cleanup complete."
