"use client";

const ROLE_COLOR_PALETTE = [
  "#ef4444", // red
  "#f97316", // orange
  "#f59e0b", // amber
  "#22c55e", // green
  "#06b6d4", // cyan
  "#3b82f6", // blue
  "#8b5cf6", // violet
  "#ec4899", // pink
] as const;

interface RoleColorPickerProps {
  value: string | null;
  onChange: (color: string | null) => void;
}

export default function RoleColorPicker({ value, onChange }: RoleColorPickerProps) {
  return (
    <div className="flex items-center gap-2 flex-wrap">
      {ROLE_COLOR_PALETTE.map((color) => {
        const isSelected = value === color;
        return (
          <button
            key={color}
            type="button"
            onClick={() => onChange(isSelected ? null : color)}
            className={`w-6 h-6 rounded-full transition-all flex-shrink-0 ${
              isSelected
                ? "ring-2 ring-offset-2 ring-slate-400 scale-110"
                : "hover:scale-110"
            }`}
            style={{ backgroundColor: color }}
            aria-label={`${isSelected ? "Deselect" : "Select"} color ${color}`}
          />
        );
      })}
      {value && (
        <button
          type="button"
          onClick={() => onChange(null)}
          className="text-xs text-slate-400 hover:text-slate-600 transition-colors"
          aria-label="Clear color"
        >
          ✕
        </button>
      )}
    </div>
  );
}
