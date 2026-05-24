"use client";

interface MiniChartProps {
  data: { label: string; value: number }[];
  height?: number;
  color?: string;
  type?: "bar" | "line";
}

export default function MiniChart({
  data,
  height = 80,
  color = "#0ea5e9",
  type = "bar",
}: MiniChartProps) {
  if (data.length === 0) return null;

  const max = Math.max(...data.map((d) => d.value), 1);

  if (type === "line") {
    const points = data
      .map((d, i) => {
        const x = data.length === 1 ? 50 : (i / (data.length - 1)) * 100;
        const y = height - (d.value / max) * (height - 8);
        return `${x},${y}`;
      })
      .join(" ");

    return (
      <svg
        viewBox={`0 0 100 ${height}`}
        className="w-full"
        preserveAspectRatio="none"
        style={{ height }}
        role="img"
        aria-label="Line chart"
      >
        <polyline
          points={points}
          fill="none"
          stroke={color}
          strokeWidth="2"
          vectorEffect="non-scaling-stroke"
        />
      </svg>
    );
  }

  return (
    <div
      className="flex items-end gap-px w-full"
      style={{ height }}
      role="img"
      aria-label="Bar chart"
    >
      {data.map((d, i) => (
        <div
          key={i}
          className="flex-1 rounded-t transition-all"
          style={{
            height: `${(d.value / max) * 100}%`,
            minHeight: d.value > 0 ? 2 : 0,
            backgroundColor: color,
            opacity: 0.7 + (d.value / max) * 0.3,
          }}
          title={`${d.label}: ${d.value}`}
        />
      ))}
    </div>
  );
}
