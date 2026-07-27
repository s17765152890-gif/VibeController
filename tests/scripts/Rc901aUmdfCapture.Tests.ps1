$projectRoot = Join-Path $PSScriptRoot '..\..\drivers\Rc901aHidFilter\umdf'
$projectPath = Join-Path $projectRoot 'Rc901aUmdfCapture.vcxproj'
$infPath = Join-Path $projectRoot 'Rc901aHidFilter.inx'
$sourcePath = Join-Path $projectRoot 'Rc901aUmdfCapture.c'
$exactHardwareId = 'BTHLEDevice\{00001812-0000-1000-8000-00805f9b34fb}_Dev_VID&010416_PID&0301_REV&0003'

Describe 'RC901A exact-device UMDF2 capture project' {
    It 'builds a UMDF2 user-mode driver DLL' {
        Test-Path -LiteralPath $projectPath | Should Be $true
        $content = Get-Content -LiteralPath $projectPath -Raw

        $content | Should Match '<UMDF_VERSION_MAJOR>2</UMDF_VERSION_MAJOR>'
        $content | Should Match '<DriverType>UMDF</DriverType>'
        $content | Should Match '<PlatformToolset>WindowsUserModeDriver10\.0</PlatformToolset>'
        $content | Should Match '<ConfigurationType>DynamicLibrary</ConfigurationType>'
        $content | Should Match '<TargetName>Rc901aUmdfCapture</TargetName>'
        $content | Should Match 'mincore\.lib'
        $content | Should Match '<FilesToPackage Include="\$\(TargetPath\)"'
        $content | Should Match '<DateStamp>07/27/2026</DateStamp>'
        $content | Should Match '<TimeStamp>1\.0\.0\.5</TimeStamp>'
    }

    It 'targets only the exact RC901A HID-over-GATT service node' {
        Test-Path -LiteralPath $infPath | Should Be $true
        $content = Get-Content -LiteralPath $infPath -Raw
        $modelLines = @($content -split "`r?`n" |
            Where-Object { $_ -match '^\s*%[^%]+%\s*=\s*[^,]+,\s*BTHLEDevice\\' })

        $modelLines.Count | Should Be 1
        $modelLines[0].ToLowerInvariant().Contains($exactHardwareId.ToLowerInvariant()) | Should Be $true
        $modelLines[0] | Should Not Match 'VID&\*|PID&\*|REV&\*'
    }

    It 'places the capture driver above the inbox HidOverGatt UMDF function driver' {
        $content = Get-Content -LiteralPath $infPath -Raw
        $orders = @([regex]::Matches($content, '(?im)^\s*UmdfServiceOrder\s*=.*$'))

        $orders.Count | Should Be 1
        $content | Should Match '(?im)^\s*DriverVer\s*=\s*07/27/2026\s*,\s*1\.0\.0\.5\s*$'
        $content | Should Not Match '(?im)^\s*Needs\s*=\s*HidBthLE\.NT\.Wdf\s*$'
        $content | Should Match '(?im)^\s*UmdfService\s*=\s*Rc901aUmdfCapture\s*,\s*Rc901aUmdfCapture\.Wdf\s*$'
        $content | Should Match '(?im)^\s*UmdfServiceOrder\s*=\s*HidOverGatt\s*,\s*Rc901aUmdfCapture\s*$'
        $content | Should Match '(?im)^\s*UmdfKernelModeClientPolicy\s*=\s*AllowKernelModeClients\s*$'
        $content | Should Match '(?im)^\s*UmdfFileObjectPolicy\s*=\s*AllowNullAndUnknownFileObjects\s*$'
        $content | Should Match '(?im)^\s*UmdfFsContextUsePolicy\s*=\s*CanUseFsContext2\s*$'
        $content | Should Match '(?im)^\s*UmdfHostPriority\s*=\s*PriorityHigh\s*$'
        $content | Should Match '(?im)^\s*UmdfMethodNeitherAction\s*=\s*Copy\s*$'
    }

    It 'replaces the diagnostic KMDF lower filter instead of stacking both filters' {
        $content = Get-Content -LiteralPath $infPath -Raw

        $content | Should Not Match '(?im)^\s*(?:AddFilter|KmdfService)\s*='
        $content | Should Not Match '(?im)^\s*HKR\s*,\s*,\s*(?:UpperFilters|LowerFilters|LowerFilterLevels)'
        $content | Should Not Match '(?im)Rc901aHidFilter\.sys'
    }

    It 'runs the DLL from the driver store and preserves every non-WDF inbox install section' {
        $content = Get-Content -LiteralPath $infPath -Raw

        $content | Should Match '(?im)^\s*Rc901aUmdfCapture\.CopyFiles\s*=\s*13\s*$'
        $content | Should Match '(?im)^\s*ServiceBinary\s*=\s*"?%13%\\Rc901aUmdfCapture\.dll"?\s*$'
        $content | Should Match '(?im)^\s*Needs\s*=\s*HidBthLE\.NT\s*$'
        $content | Should Match '(?im)^\s*Needs\s*=\s*HidBthLE\.NT\.hw\s*$'
        $content | Should Match '(?im)^\s*Needs\s*=\s*HidBthLE\.NT\.Services\s*$'
        $content | Should Match '(?im)^\s*Needs\s*=\s*HidBthLE\.NT\.CoInstallers\s*$'
        $content | Should Match '(?im)^\s*ExcludeFromSelect\s*=\s*\*\s*$'
        $content | Should Match '(?im)^\s*PnpLockdown\s*=\s*1\s*$'
    }
}

Describe 'RC901A UMDF report-descriptor capture contract' {
    It 'is a passive exact pass-through filter with a parallel queue' {
        Test-Path -LiteralPath $sourcePath | Should Be $true
        $content = Get-Content -LiteralPath $sourcePath -Raw

        $content | Should Match 'WdfFdoInitSetFilter'
        $content | Should Match 'WdfExecutionLevelPassive'
        $content | Should Match 'WdfIoQueueDispatchParallel'
        $content | Should Match 'EvtIoDeviceControl'
        $content | Should Match 'IOCTL_HID_GET_REPORT_DESCRIPTOR'
        $content | Should Match 'hidumdf.*DEVICE_CONTROL'
        $content | Should Not Match 'EvtIoInternalDeviceControl\s*='
    }

    It 'forwards the current request and observes the lower completion' {
        $content = Get-Content -LiteralPath $sourcePath -Raw

        $content | Should Match 'WdfRequestFormatRequestUsingCurrentType'
        $content | Should Match 'WdfRequestSetCompletionRoutine'
        $content | Should Match 'WdfRequestSend'
        $content | Should Match 'WdfRequestRetrieveOutputBuffer'
        $content | Should Match 'Params->IoStatus\.Status'
        $content | Should Match 'Params->IoStatus\.Information'
        $content | Should Match 'WdfRequestCompleteWithInformation'
    }

    It 'persists a bounded copy and SHA-256 outside the completion callback' {
        $content = Get-Content -LiteralPath $sourcePath -Raw

        $content | Should Match 'RC901A_MAX_REPORT_DESCRIPTOR_SIZE'
        $content | Should Match 'WdfWorkItemEnqueue'
        $content | Should Match 'WdfDriverOpenParametersRegistryKey'
        $content | Should Match 'KEY_READ\s*\|\s*KEY_SET_VALUE'
        $content | Should Match 'Rc901aCapturedReportDescriptor'
        $content | Should Match 'Rc901aCapturedReportDescriptorSha256'
        $content | Should Match 'Rc901aDescriptorRepairStatus'
        $content | Should Match 'WdfRegistryRemoveValue'
    }

    It 'atomically preserves the raw capture before applying the fingerprint-gated repair' {
        $content = Get-Content -LiteralPath $sourcePath -Raw
        $recordStart = $content.IndexOf('Rc901aRecordDescriptorResult(')
        $recordEnd = $content.IndexOf('static VOID', $recordStart + 1)
        $recordBody = $content.Substring($recordStart, $recordEnd - $recordStart)
        $lockIndex = $recordBody.IndexOf('WdfSpinLockAcquire')
        $copyIndex = $recordBody.IndexOf('Rc901aCopyDescriptor')
        $repairIndex = $recordBody.IndexOf('Rc901aRepairReportDescriptor')
        $repairStateIndex = $recordBody.IndexOf('Context->RepairStatus')
        $unlockIndex = $recordBody.IndexOf('WdfSpinLockRelease')

        $content | Should Not Match 'Rc901aRecordDescriptorRepairResult'
        $lockIndex | Should BeGreaterThan -1
        $copyIndex | Should BeGreaterThan $lockIndex
        $repairIndex | Should BeGreaterThan $copyIndex
        $repairStateIndex | Should BeGreaterThan $repairIndex
        $unlockIndex | Should BeGreaterThan $repairStateIndex
    }

    It 'never lets capture or repair replace the lower completion result' {
        $content = Get-Content -LiteralPath $sourcePath -Raw

        $content | Should Not Match 'WdfRequestCompleteWithInformation\s*\(\s*Request\s*,\s*STATUS_SUCCESS'
        $content | Should Match 'WdfRequestCompleteWithInformation\s*\(\s*Request\s*,\s*lowerStatus\s*,\s*lowerInformation'
    }
}

Describe 'RC901A UMDF input-report capture contract' {
    It 'observes read-report completions without changing ordinary pass-through traffic' {
        $content = Get-Content -LiteralPath $sourcePath -Raw

        $content | Should Match 'IOCTL_HID_READ_REPORT'
        $content | Should Match 'IOCTL_HID_GET_INPUT_REPORT'
        $content | Should Match 'Rc901aEvtInputReportComplete'
        $content | Should Match 'Rc901aRecordInputReport'
        $content | Should Match 'WdfRequestRetrieveOutputBuffer'
        $content | Should Match 'WdfRequestCompleteWithInformation\s*\(\s*Request\s*,\s*lowerStatus\s*,\s*lowerInformation'
        $content | Should Match 'if\s*\(\s*IoControlCode\s*!=\s*IOCTL_HID_GET_REPORT_DESCRIPTOR'
        $content | Should Match 'Rc901aForwardAndForget'
    }

    It 'persists a bounded chronological report history outside the completion callback' {
        $content = Get-Content -LiteralPath $sourcePath -Raw

        $content | Should Match 'RC901A_INPUT_REPORT_HISTORY_CAPACITY'
        $content | Should Match 'Rc901aCopyInputReportHistory'
        $content | Should Match 'Rc901aInputReportHistory'
        $content | Should Match 'Rc901aInputReportCount'
        $content | Should Match 'Rc901aInputReportTotal'
        $content | Should Match 'WdfWorkItemEnqueue'
    }
}
