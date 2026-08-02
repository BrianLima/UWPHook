$installedapps = get-AppxPackage
$invalidNames = '*ms-resource*', '*DisplayName*'
$aumidList = @()

foreach ($app in $installedapps)
{
    try {
            if(-not $app.IsFramework){
            foreach ($id in (Get-AppxPackageManifest $app).package.applications.application.id)
            {
                    $appx = Get-AppxPackageManifest $app;
                    $name = $appx.Package.Properties.DisplayName;
                    $executable = $appx.Package.Applications.Application.Executable;

                    # Handle app running with microsoft launcher or which doesn't have an executable in the manifest
                    if([string]::IsNullOrWhitespace($executable) -or $executable -eq "GameLaunchHelper.exe") {
                        if(Test-Path -Path ($app.InstallLocation + "\MicrosoftGame.Config")) {
                            [xml]$msconfig = Get-Content ($app.InstallLocation + "\MicrosoftGame.Config");
                            $executable = $msconfig.Game.ExecutableList.Executable.Name;
                        }
                        else {
                            # Cannot handle app which doesn't have any configuration to read
                            continue;
                        }
                    }
                    # Convert object to ensure is the String of execuble (cf Halo Master Chief Collection example below)
                    # mcclauncher.exe
                    # MCC\Binaries\Win64\MCCWinStore-Win64-Shipping.exe
                    if($executable -is [Object[]]) { $executable = $executable[1].ToString() }

                    # Exclude apps without a name acceptable
                    if($name -like '*DisplayName*' -or $name  -like '*ms-resource*')
                    {
                        continue;
                    }

                    $logo = $app.InstallLocation + "\" + $appx.Package.Applications.Application.VisualElements.Square150x150Logo;
                    
                    # Check for possible duplicate game like Halo MCC which have two versions (one with AC and one without AC)
                    $duplicate = $false;
                    foreach($item in $aumidList) {
                        # Format: name|logo|packageFamily!id|executable;
                        $parts = $item -split '\|'
                        $existingName = $parts[0]
                        $existingPkgFamily = ($parts[2] -split '!')[0]

                        # Consider duplicate only if both name and package family match
                        if($existingName -eq $name -and $existingPkgFamily -eq $app.packagefamilyname) {
                            $duplicate = $true
                            break
                        }
                    }

                    # Insert if not duplicated
                    if(!$duplicate) {
                        $aumidList += $name + "|" + $logo + "|" + $app.packagefamilyname + "!" + $id + "|" + $executable + ";"
                    }
                }
            }
        }
        catch
        {
            $ErrorMessage = $_.Exception.Message
            $FailedItem = $_.Exception.ItemName
        }
}

$aumidList;