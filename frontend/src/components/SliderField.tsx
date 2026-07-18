interface SliderFieldProps {
  label: string;
  value: number;
  min: number;
  max: number;
  unit?: string;
  onChange(value: number): void;
}

export function SliderField({ label, value, min, max, unit = "%", onChange }: SliderFieldProps) {
  const inputId = `slider-${label}`;
  return (
    <div className="slider-field">
      <span><label htmlFor={inputId}>{label}</label><output>{value}{unit}</output></span>
      <input
        id={inputId}
        type="range"
        min={min}
        max={max}
        value={value}
        onChange={(event) => onChange(Number(event.target.value))}
      />
    </div>
  );
}
