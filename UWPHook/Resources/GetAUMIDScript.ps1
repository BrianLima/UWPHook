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
                    $executable = (Select-Xml -Path ($app.InstallLocation + "\MicrosoftGame.Config") -XPath "/Game/ExecutableList/Executable/@Name").Node.Value
                    # Convert object to ensure is the String of execuble (cf Halo Master Chief Collection example below)
                    # mcclauncher.exe
                    # MCC\Binaries\Win64\MCCWinStore-Win64-Shipping.exe
                    if($executable -is [Object[]]) { $executable = $executable[1].ToString() }

                    if($name -like '*DisplayName*' -or $name  -like '*ms-resource*')
                    {
                        $name = $appx.Package.Applications.Application.VisualElements.DisplayName;
                    }
                    if($name -like '*DisplayName*' -or $name  -like '*ms-resource*')
                    {
                        $name = "App name not found, double click here to edit it";
                    }

                    $logo = $app.InstallLocation + "\" + $appx.Package.Applications.Application.VisualElements.Square150x150Logo;

                    $aumidList += $name + "|" + $logo + "|" + $app.packagefamilyname + "!" + $id + "|" + $executable + ";"
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