import { useEffect, useState } from "react";
import {
  AlertTriangle,
  Check,
  ChevronDown,
  LoaderCircle,
  RadioTower,
  RefreshCw,
  RotateCcw,
  ShieldCheck,
  X,
} from "lucide-react";
import type {
  Rc901aControl,
  Rc901aInputSignal,
  Rc901aInputStatus,
  Rc901aLearnableControl,
} from "../app/types";

interface RemoteKeyPresentation {
  control: Rc901aControl;
  label: string;
  keycap: string;
  learnable?: false;
}

const remoteKeys: readonly RemoteKeyPresentation[] = [
  { control: "remotePower", label: "电源键", keycap: "⏻", learnable: false },
  { control: "remoteMute", label: "静音键", keycap: "MUTE" },
  { control: "remoteInput", label: "输入源键", keycap: "INPUT" },
  { control: "remoteRed", label: "红色键", keycap: "R" },
  { control: "remoteGreen", label: "绿色键", keycap: "G" },
  { control: "remoteBlue", label: "蓝色键", keycap: "B" },
  { control: "remoteUp", label: "方向键上", keycap: "↑" },
  { control: "remoteLeft", label: "方向键左", keycap: "←" },
  { control: "remoteOk", label: "确认键", keycap: "OK" },
  { control: "remoteRight", label: "方向键右", keycap: "→" },
  { control: "remoteDown", label: "方向键下", keycap: "↓" },
  { control: "remoteBack", label: "返回键", keycap: "↩" },
  { control: "remoteVolumeUp", label: "音量 +", keycap: "VOL+" },
  { control: "remoteHome", label: "主页键", keycap: "HOME" },
  { control: "remoteMenu", label: "菜单键", keycap: "MENU" },
  { control: "remoteVolumeDown", label: "音量 −", keycap: "VOL−" },
  { control: "remoteSettings", label: "设置键", keycap: "SET" },
  { control: "remoteApp1", label: "bilibili 键", keycap: "bilibili" },
  { control: "remoteApp2", label: "奇异果 TV 键", keycap: "奇异果" },
  { control: "remoteMic", label: "麦克风键", keycap: "MIC" },
  { control: "remoteBrightnessUp", label: "亮度 +", keycap: "☀+" },
  { control: "remoteBrightnessDown", label: "亮度 −", keycap: "☀−" },
  { control: "remotePictureMode", label: "图像模式", keycap: "PIC" },
];

const keyByControl = new Map<string, RemoteKeyPresentation>(
  remoteKeys.map((key) => [key.control, key]),
);

const idleInput: Rc901aInputStatus = {
  bindings: [],
  lastUnknown: null,
  learning: {
    phase: "idle",
    sessionId: null,
    target: null,
    candidate: null,
    conflict: null,
    expiresAt: null,
  },
};

interface Rc901aLearningPanelProps {
  inputStatus?: Rc901aInputStatus;
  learningEnabled?: boolean;
  learningUnavailableReason?: string;
  onStart?(
    control: Rc901aLearnableControl,
    compatibilityOverride: true,
  ): void;
  onConfirm?(sessionId: string): void;
  onRetry?(sessionId: string): void;
  onCancel?(sessionId: string): void;
  onReset?(): void;
}

function labelFor(control: string | null) {
  if (!control) return "这个按键";
  return keyByControl.get(control)?.label ?? control;
}

function formatSignal(signal: Rc901aInputSignal) {
  const code = signal.code.toString(16).toUpperCase().padStart(4, "0");
  return `${signal.kind} · 0x${code}`;
}

export function Rc901aLearningPanel({
  inputStatus = idleInput,
  learningEnabled = true,
  learningUnavailableReason,
  onStart,
  onConfirm,
  onRetry,
  onCancel,
  onReset,
}: Rc901aLearningPanelProps) {
  const { learning, bindings } = inputStatus;
  const [expanded, setExpanded] = useState(false);
  const [confirmingReset, setConfirmingReset] = useState(false);
  const [confirmingReassignment, setConfirmingReassignment] = useState(false);
  const [confirmingVerifiedOverride, setConfirmingVerifiedOverride] =
    useState(false);
  const isLearning = learning.phase !== "idle";
  const isOpen = expanded || isLearning;
  const learnedControls = new Set(
    bindings
      .filter((binding) => binding.source === "learned")
      .map((binding) => binding.control),
  );
  const learnedCount = learnedControls.size;
  const verifiedControls = new Set(
    bindings
      .filter((binding) => binding.source === "verifiedDefault")
      .map((binding) => binding.control),
  );
  const targetLabel = labelFor(learning.target);
  const conflictLabel = labelFor(learning.conflict?.control ?? null);
  const sessionId = learning.sessionId;
  const verifiedConflict = learning.conflict?.source === "verifiedDefault";
  const learnedReassignment =
    learning.conflict?.source === "learned" &&
    learning.conflict.control !== learning.target;
  const sameControlReview =
    learning.conflict?.source === "learned" &&
    learning.conflict.control === learning.target;

  useEffect(() => {
    setConfirmingReassignment(false);
    setConfirmingVerifiedOverride(false);
  }, [
    learning.sessionId,
    learning.candidate?.kind,
    learning.candidate?.code,
  ]);

  useEffect(() => {
    if (learnedCount === 0) setConfirmingReset(false);
  }, [learnedCount]);

  useEffect(() => {
    if (isLearning) setConfirmingReset(false);
  }, [isLearning]);

  const cancel = () => {
    if (sessionId) onCancel?.(sessionId);
  };
  const retry = () => {
    if (sessionId) onRetry?.(sessionId);
  };
  const confirm = () => {
    if (sessionId) onConfirm?.(sessionId);
  };

  return (
    <section className="rc901a-learning-panel" aria-label="RC901A 按键配置">
      <div className="rc901a-profile-summary">
        <span className="rc901a-profile-summary-icon" aria-hidden="true">
          <ShieldCheck size={18} />
        </span>
        <span>
          <strong>22 个已验证按键已自动就绪</strong>
          <small>
            专用驱动会直接识别已验证键码，无需逐键学习。电源键尚未验证，当前保持停用。
          </small>
        </span>
        <span className="rc901a-profile-badge">自动配置</span>
      </div>

      <button
        className="rc901a-learning-trigger"
        type="button"
        aria-label="兼容性按键识别"
        aria-expanded={isOpen}
        onClick={() => {
          if (!isLearning) setExpanded((current) => !current);
        }}
      >
        <span className="rc901a-learning-trigger-icon" aria-hidden="true">
          <RadioTower size={17} />
        </span>
        <span>
          <strong>兼容性按键识别</strong>
          <small>仅在不同固件键位不一致，或需要自定义覆盖时使用。</small>
        </span>
        <ChevronDown
          className="rc901a-learning-chevron"
          data-open={isOpen}
          size={16}
          aria-hidden="true"
        />
      </button>

      {isOpen && (
        <div className="rc901a-learning-content">
          <p className="rc901a-learning-intro">
            这里会覆盖自动识别结果。请选择一个按键，再按遥控器上的对应键；
            未遇到键位问题时无需使用。电源键尚无可靠键码，因此暂不开放识别。
          </p>

          {!learningEnabled && (
            <p className="rc901a-learning-unavailable" role="note">
              <AlertTriangle size={15} aria-hidden="true" />
              <span>
                <strong>{learningUnavailableReason ?? "暂时无法识别按键"}</strong>
                <small>当前按键状态仍可查看，满足条件后即可逐项识别。</small>
              </span>
            </p>
          )}

          {isLearning && (
            <div
              className="rc901a-learning-session"
              data-phase={learning.phase}
              role="status"
              aria-live="polite"
              aria-atomic="true"
            >
              {learning.phase === "awaitingPress" && (
                <>
                  <span className="rc901a-learning-pulse" aria-hidden="true" />
                  <div>
                    <small>正在监听</small>
                    <strong>请按遥控器上的{targetLabel}</strong>
                    <p>只需正常按下一次，VibeController 会暂时拦截输入，不会执行原映射。</p>
                  </div>
                  <button className="quiet-button" type="button" onClick={cancel}>
                    <X size={14} />取消识别
                  </button>
                </>
              )}

              {learning.phase === "awaitingRelease" && (
                <>
                  <span className="rc901a-learning-pulse" data-captured="true" aria-hidden="true" />
                  <div>
                    <small>信号已捕获</small>
                    <strong>已检测到按下，请松开{targetLabel}</strong>
                    <p>松开后再核对信号，避免把长按重复输入误认为多个按键。</p>
                  </div>
                  <button className="quiet-button" type="button" onClick={cancel}>
                    <X size={14} />取消识别
                  </button>
                </>
              )}

              {learning.phase === "review" && learning.candidate && (
                <div className="rc901a-learning-review">
                  <div className="rc901a-review-heading">
                    <div>
                      <small>检测结果 · {targetLabel}</small>
                      <strong>{formatSignal(learning.candidate)}</strong>
                    </div>
                    <span className="rc901a-captured-badge"><Check size={13} />已捕获</span>
                  </div>

                  {verifiedConflict && (
                    <p className="rc901a-learning-warning" role="alert">
                      <AlertTriangle size={15} />
                      这个信号当前属于系统默认的{conflictLabel}。继续后，将用它覆盖
                      {targetLabel}的自动识别。
                    </p>
                  )}

                  {learnedReassignment && (
                    <p className="rc901a-learning-warning" role="alert">
                      <AlertTriangle size={15} />
                      这个信号目前分配给{conflictLabel}。如继续，原按键会变为未学习。
                    </p>
                  )}

                  <div className="rc901a-review-actions">
                    <button className="quiet-button" type="button" onClick={retry}>
                      <RefreshCw size={14} />重新检测
                    </button>
                    <button className="quiet-button" type="button" onClick={cancel}>
                      <X size={14} />取消识别
                    </button>
                    {(!learning.conflict || sameControlReview) && (
                      <button className="primary-button" type="button" onClick={confirm}>
                        <Check size={14} />确认识别
                      </button>
                    )}
                    {verifiedConflict && !confirmingVerifiedOverride && (
                      <button
                        className="primary-button"
                        type="button"
                        onClick={() => setConfirmingVerifiedOverride(true)}
                      >
                        继续覆盖默认识别
                      </button>
                    )}
                    {verifiedConflict && confirmingVerifiedOverride && (
                      <button
                        className="danger-confirm-button"
                        type="button"
                        onClick={confirm}
                      >
                        确认覆盖默认识别
                      </button>
                    )}
                    {learnedReassignment && !confirmingReassignment && (
                      <button
                        className="primary-button"
                        type="button"
                        onClick={() => setConfirmingReassignment(true)}
                      >
                        继续重新分配
                      </button>
                    )}
                    {learnedReassignment && confirmingReassignment && (
                      <button
                        className="danger-confirm-button"
                        type="button"
                        onClick={confirm}
                      >
                        确认重新分配
                      </button>
                    )}
                  </div>
                </div>
              )}

              {learning.phase === "saving" && (
                <>
                  <LoaderCircle className="rc901a-saving-icon" size={18} aria-hidden="true" />
                  <div aria-live="polite">
                    <small>正在保存</small>
                    <strong>正在保存{targetLabel}的兼容性识别结果</strong>
                    <p>完成后可继续识别下一个按键。</p>
                  </div>
                </>
              )}
            </div>
          )}

          <div
            className="rc901a-key-learning-list"
            aria-label="遥控器按键识别状态"
            role="list"
          >
            {remoteKeys.map((key) => {
              const learned = learnedControls.has(key.control);
              const verified = verifiedControls.has(key.control);
              const status = verified
                ? "已验证"
                : learned
                  ? "已自定义"
                  : "尚未验证";
              const active = learning.target === key.control && isLearning;

              return (
                <div
                  className="rc901a-key-learning-row"
                  data-state={verified ? "verified" : learned ? "learned" : "unlearned"}
                  data-active={active}
                  key={key.control}
                  role="listitem"
                >
                  <span className="rc901a-key-learning-keycap">{key.keycap}</span>
                  <span className="rc901a-key-learning-name">
                    <strong>{key.label}</strong>
                    <small>{status}</small>
                  </span>
                  {key.learnable !== false && (
                    <button
                      type="button"
                      aria-label={`${verified || learned ? "重新" : ""}识别${key.label}`}
                      disabled={isLearning || !learningEnabled || !onStart}
                      onClick={() =>
                        onStart?.(
                          key.control as Rc901aLearnableControl,
                          true,
                        )
                      }
                    >
                      {verified || learned ? "重新识别" : "识别"}
                    </button>
                  )}
                </div>
              );
            })}
          </div>

          {learnedCount > 0 && (
            <div className="rc901a-reset-row">
              <span>
                <strong>已保存 {learnedCount} 个兼容性覆盖</strong>
                <small>重置只会清除兼容性覆盖，22 键自动配置不受影响。</small>
              </span>
              {confirmingReset ? (
                <span className="rc901a-reset-confirm">
                  <button
                    className="quiet-button"
                    type="button"
                    onClick={() => setConfirmingReset(false)}
                  >
                    取消
                  </button>
                  <button
                    className="danger-confirm-button"
                    type="button"
                    disabled={isLearning}
                    onClick={() => {
                      setConfirmingReset(false);
                      onReset?.();
                    }}
                  >
                    确认重置
                  </button>
                </span>
              ) : (
                <button
                  className="quiet-button"
                  type="button"
                  disabled={isLearning || !onReset}
                  onClick={() => setConfirmingReset(true)}
                >
                    <RotateCcw size={14} />重置兼容性覆盖
                </button>
              )}
            </div>
          )}
        </div>
      )}
    </section>
  );
}
