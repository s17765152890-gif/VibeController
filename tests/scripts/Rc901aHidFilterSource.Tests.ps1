$sourcePath = Join-Path $PSScriptRoot '..\..\drivers\Rc901aHidFilter\driver\Device.c'

Describe 'RC901A KMDF IRP forwarding contract' {
    It 'observes both HID control paths around the UMDF translation boundary' {
        $content = Get-Content -LiteralPath $sourcePath -Raw
        $registrations = [regex]::Matches(
            $content,
            'WdfDeviceInitAssignWdmIrpPreprocessCallback\s*\(\s*DeviceInit\s*,\s*Rc901aEvtWdmIrpPreprocess\s*,\s*(IRP_MJ_(?:INTERNAL_)?DEVICE_CONTROL)'
        )

        $registrations.Count | Should Be 2
        @($registrations | ForEach-Object { $_.Groups[1].Value }) | Should Be @(
            'IRP_MJ_INTERNAL_DEVICE_CONTROL',
            'IRP_MJ_DEVICE_CONTROL'
        )
    }

    It 'persists attach request and completion diagnostics beside the captured descriptor' {
        $content = Get-Content -LiteralPath $sourcePath -Raw

        $content | Should Match 'WdfDriverOpenParametersRegistryKey'
        $content | Should Not Match 'PLUGPLAY_REGKEY_DEVICE'
        $content | Should Match 'Rc901aFilterAttached'
        $content | Should Match 'Rc901aObservedRequestCount'
        $content | Should Match 'Rc901aLastMajorFunction'
        $content | Should Match 'Rc901aLastIoControlCode'
        $content | Should Match 'Rc901aCompletionCount'
        $content | Should Match 'Rc901aLastCompletionStatus'
        $content | Should Match 'Rc901aLastCompletionInformation'
    }

    It 'returns both target and non-target requests through the KMDF preprocessed dispatcher' {
        $content = Get-Content -LiteralPath $sourcePath -Raw
        $dispatchCalls = [regex]::Matches($content, 'WdfDeviceWdmDispatchPreprocessedIrp\s*\(\s*Device\s*,\s*Irp\s*\)')

        $dispatchCalls.Count | Should Be 2
        $content | Should Not Match '\bIoCallDriver\s*\('
        $content | Should Not Match '\bWdfDeviceWdmGetAttachedDevice\s*\('
    }

    It 'skips the added stack location when no postprocessing is needed' {
        $content = Get-Content -LiteralPath $sourcePath -Raw

        $content | Should Match 'IoSkipCurrentIrpStackLocation\s*\(\s*Irp\s*\)'
    }

    It 'copies the stack and registers completion before target postprocessing' {
        $content = Get-Content -LiteralPath $sourcePath -Raw

        $content | Should Match 'IoCopyCurrentIrpStackLocationToNext\s*\(\s*Irp\s*\)'
        $content | Should Match 'IoSetCompletionRoutine\s*\('
    }
}
