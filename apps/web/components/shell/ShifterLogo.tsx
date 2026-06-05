"use client";

import Image from "next/image";

type ShifterLogoVariant = "icon" | "full";

interface ShifterLogoProps {
  size?: number;
  variant?: ShifterLogoVariant;
  className?: string;
}

const LOGO_ASPECT_RATIOS: Record<ShifterLogoVariant, number> = {
  icon: 464 / 465,
  full: 1059 / 294,
};

const LOGO_SOURCES: Record<ShifterLogoVariant, string> = {
  icon: "/shifter_icon.png",
  full: "/shifter_full_logo.png",
};

/**
 * Image-backed Shifter brand mark.
 *
 * size is the rendered height. The width is derived from the asset ratio.
 */
export default function ShifterLogo({
  size = 32,
  variant = "icon",
  className,
}: ShifterLogoProps) {
  const width = Math.round(size * LOGO_ASPECT_RATIOS[variant]);

  return (
    <Image
      src={LOGO_SOURCES[variant]}
      alt="Shifter"
      width={width}
      height={size}
      className={className}
      style={{
        width,
        height: size,
        objectFit: "contain",
        flexShrink: 0,
      }}
    />
  );
}
