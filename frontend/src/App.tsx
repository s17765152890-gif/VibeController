import { useState } from "react";
import { AppShell } from "./components/AppShell";
import type { AppPage } from "./components/AppShell";
import { Dashboard } from "./pages/Dashboard";
import { MappingEditor } from "./pages/MappingEditor";
import { Settings } from "./pages/Settings";
import { appBridge } from "./app/AppBridge";
import { useRuntimeState } from "./app/useRuntimeState";

export default function App() {
  const [state, setState] = useRuntimeState();
  const [page, setPage] = useState<AppPage>("dashboard");

  const toggleMapping = (enabled: boolean) => {
    setState((current) => ({ ...current, mappingEnabled: enabled }));
    appBridge.send("setMappingEnabled", { enabled });
  };

  const toggleTestMode = (enabled: boolean) => {
    setState((current) => ({ ...current, testMode: enabled }));
    appBridge.send("setTestMode", { enabled });
  };

  return (
    <AppShell page={page} onNavigate={setPage}>
      {page === "dashboard" && <Dashboard state={state} onToggleMapping={toggleMapping} onToggleTestMode={toggleTestMode} />}
      {page === "mapping" && (
        <MappingEditor
          controllerType={state.configuration?.controllerType}
          initialValues={state.configuration?.mappings}
          onSave={(mapping) => appBridge.send("updateMapping", mapping)}
          onReset={() => appBridge.send("resetDefaults", {})}
        />
      )}
      {page === "settings" && (
        <Settings
          initialValues={state.configuration ? {
            controllerType: state.configuration.controllerType,
            activeControllerIndex: state.configuration.activeControllerIndex,
            codexOnly: state.configuration.codexOnly,
            dictationShortcut: state.configuration.dictationShortcut,
            mouseSpeed: Math.round(state.configuration.mouseSpeed),
            scrollSpeed: Math.round(state.configuration.scrollSpeed),
            deadZone: state.configuration.deadZone,
            startWithWindows: state.configuration.startWithWindows,
            codexLightbarEnabled: state.configuration.codexLightbarEnabled ?? false,
          } : undefined}
          microphone={state.configuration?.microphone}
          codexHook={state.configuration?.codexHook}
          codexActivity={state.configuration?.codexActivity}
          rc901a={state.configuration?.rc901a}
          rc901aInput={state.configuration?.rc901aInput}
          persistedControllerType={state.configuration?.controllerType}
          rc901aLearningReady={
            state.configuration?.rc901a?.connectionState === "connected" ||
            state.configuration?.rc901a?.connectionState === "connectedLimited"
          }
          onSave={(settings) => {
            setState((current) => ({
              ...current,
              lastAction: settings.controllerType === current.configuration?.controllerType
                ? current.lastAction
                : null,
              configuration: current.configuration ? {
                ...current.configuration,
                ...settings,
                controllerType: current.configuration.controllerType,
              } : current.configuration,
            }));
            appBridge.send("updateSettings", settings);
          }}
          onRefreshIntegrations={() => appBridge.send("refreshIntegrations", {})}
          onRefreshRc901a={() => appBridge.send("refreshRc901a", {})}
          onClearRc901aSamples={() => appBridge.send("clearRc901aSamples", {})}
          onStartRc901aLearning={(control, compatibilityOverride) =>
            appBridge.send("startRc901aLearning", {
              control,
              compatibilityOverride,
            })
          }
          onConfirmRc901aLearning={(sessionId) => appBridge.send("confirmRc901aLearning", { sessionId })}
          onRetryRc901aLearning={(sessionId) => appBridge.send("retryRc901aLearning", { sessionId })}
          onCancelRc901aLearning={(sessionId) => appBridge.send("cancelRc901aLearning", { sessionId })}
          onResetRc901aLearnedBindings={() => appBridge.send("resetRc901aLearnedBindings", {})}
          onCopyDiagnostics={() => {
            void navigator.clipboard?.writeText(JSON.stringify({ version: "0.1.0", runtime: state }, null, 2));
            appBridge.send("requestState", { copyDiagnostics: true });
          }}
        />
      )}
    </AppShell>
  );
}
