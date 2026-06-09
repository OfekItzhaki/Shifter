"use client";

import ShifterLogo from "@/components/shell/ShifterLogo";

export default function AuthBrand() {
  return (
    <div className="auth-brand-lockup" aria-label="Shifter">
      <span className="auth-brand-logo-chip">
        <ShifterLogo size={36} variant="icon" className="auth-brand-logo" />
      </span>
      <span className="auth-brand-copy">
        <span className="auth-brand-name">Shifter</span>
        <span className="auth-brand-tagline">Smart Shift Scheduling</span>
      </span>
    </div>
  );
}
