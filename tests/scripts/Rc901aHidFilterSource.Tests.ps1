$sourcePath = Join-Path $PSScriptRoot '..\..\drivers\Rc901aHidFilter\driver\Device.c'

Describe 'RC901A KMDF IRP forwarding contract' {
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
