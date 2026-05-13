import { render, screen, fireEvent } from "@testing-library/react";
import { describe, it, expect, vi } from "vitest";
import BalanceSlider from "@/components/home-leave/BalanceSlider";

describe("BalanceSlider", () => {
  it("renders with Hebrew labels", () => {
    render(<BalanceSlider value={50} onChange={() => {}} />);

    expect(screen.getByText("יותר אנשים בבסיס")).toBeInTheDocument();
    expect(screen.getByText("יותר אנשים בבית")).toBeInTheDocument();
  });

  it("displays the current numeric value", () => {
    render(<BalanceSlider value={73} onChange={() => {}} />);

    expect(screen.getByText("73")).toBeInTheDocument();
  });

  it("calls onChange when slider value changes", () => {
    const onChange = vi.fn();
    render(<BalanceSlider value={50} onChange={onChange} />);

    const slider = screen.getByRole("slider");
    fireEvent.change(slider, { target: { value: "75" } });

    expect(onChange).toHaveBeenCalledWith(75);
  });

  it("clamps values to 0–100 range", () => {
    const onChange = vi.fn();
    render(<BalanceSlider value={50} onChange={onChange} />);

    const slider = screen.getByRole("slider");

    fireEvent.change(slider, { target: { value: "150" } });
    expect(onChange).toHaveBeenCalledWith(100);

    fireEvent.change(slider, { target: { value: "-10" } });
    expect(onChange).toHaveBeenCalledWith(0);
  });

  it("supports Page Up/Down for ±10 increments", () => {
    const onChange = vi.fn();
    render(<BalanceSlider value={50} onChange={onChange} />);

    const slider = screen.getByRole("slider");

    fireEvent.keyDown(slider, { key: "PageUp" });
    expect(onChange).toHaveBeenCalledWith(60);

    fireEvent.keyDown(slider, { key: "PageDown" });
    expect(onChange).toHaveBeenCalledWith(40);
  });

  it("clamps Page Up at 100", () => {
    const onChange = vi.fn();
    render(<BalanceSlider value={95} onChange={onChange} />);

    const slider = screen.getByRole("slider");
    fireEvent.keyDown(slider, { key: "PageUp" });

    expect(onChange).toHaveBeenCalledWith(100);
  });

  it("clamps Page Down at 0", () => {
    const onChange = vi.fn();
    render(<BalanceSlider value={5} onChange={onChange} />);

    const slider = screen.getByRole("slider");
    fireEvent.keyDown(slider, { key: "PageDown" });

    expect(onChange).toHaveBeenCalledWith(0);
  });

  it("has correct ARIA attributes", () => {
    render(<BalanceSlider value={42} onChange={() => {}} />);

    const slider = screen.getByRole("slider");
    expect(slider).toHaveAttribute("aria-valuemin", "0");
    expect(slider).toHaveAttribute("aria-valuemax", "100");
    expect(slider).toHaveAttribute("aria-valuenow", "42");
    expect(slider).toHaveAttribute("aria-label", "איזון חופשות בית-בסיס");
  });

  it("is disabled when disabled prop is true", () => {
    render(<BalanceSlider value={50} onChange={() => {}} disabled />);

    const slider = screen.getByRole("slider");
    expect(slider).toBeDisabled();
  });

  it("does not call onChange on Page Up/Down when already at boundary", () => {
    const onChange = vi.fn();
    render(<BalanceSlider value={100} onChange={onChange} />);

    const slider = screen.getByRole("slider");
    fireEvent.keyDown(slider, { key: "PageUp" });

    // value is already 100, clamp(100+10) = 100, same as current → no call
    expect(onChange).not.toHaveBeenCalled();
  });
});
