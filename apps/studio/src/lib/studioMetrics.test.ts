import { describe, expect, it } from "vitest";
import { formatClock, gainLabel, viewStatus } from "./studioMetrics";

const idle = {
  demo: false,
  isLive: false,
  isRecording: false,
  liveSeconds: 0,
  recordSeconds: 0,
  cpuPercent: null as number | null,
  videoFps: 0,
  bitrateMbps: 0,
  lanUrl: null as string | null,
};

describe("viewStatus", () => {
  it("shows a silent studio when nothing is connected", () => {
    const view = viewStatus(idle);
    expect(view.exampleData).toBe(false);
    expect(view.liveOn).toBe(false);
    expect(view.recOn).toBe(false);
    expect(view.onAir).toBe(false);
    expect(view.liveTime).toBe("00:00:00");
    expect(view.recTime).toBe("00:00:00");
    expect(view.programClock).toBe("");
    expect(view.programFooter).toBe("En attente");
    expect(view.bitrate).toBe("0.00 Mbps");
    expect(view.fps).toBe("0.00 FPS");
    expect(view.cpu).toBe("—");
    expect(view.lan).toBe("non détecté");
  });

  it("does not adopt example figures just because they were passed in", () => {
    const view = viewStatus({
      ...idle,
      liveSeconds: 12 * 60 + 34,
      bitrateMbps: 6.42,
      videoFps: 60,
      cpuPercent: 4.8,
      lanUrl: "http://192.168.46.78:8080/live",
    });
    expect(view.exampleData).toBe(false);
    expect(view.liveOn).toBe(false);
    expect(view.liveTime).toBe("00:00:00");
    expect(view.bitrate).toBe("0.00 Mbps");
    expect(view.onAir).toBe(false);
    expect(view.programFooter).toBe("En attente");
  });

  it("follows a real live session without substituting the reference clock", () => {
    const view = viewStatus({
      ...idle,
      isLive: true,
      isRecording: true,
      liveSeconds: 5,
      recordSeconds: 9,
      cpuPercent: 12.25,
      videoFps: 24.5,
      bitrateMbps: 2.5,
      lanUrl: "http://10.0.0.8:8089/live",
    });
    expect(view.exampleData).toBe(false);
    expect(view.liveTime).toBe("00:00:05");
    expect(view.recTime).toBe("00:00:09");
    expect(view.programClock).toBe("00:00:05");
    expect(view.programFooter).toBe("Diffusion en cours...");
    expect(view.cpu).toBe("12.3%");
    expect(view.fps).toBe("24.50 FPS");
    expect(view.bitrate).toBe("2.50 Mbps");
    expect(view.lan).toBe("http://10.0.0.8:8089/live");
    expect(view.onAir).toBe(true);
  });

  it("exposes the reference sample only in explicit demo mode", () => {
    const view = viewStatus({ ...idle, demo: true });
    expect(view.exampleData).toBe(true);
    expect(view.liveTime).toBe("00:12:34");
    expect(view.recTime).toBe("00:00:00");
    expect(view.cpu).toBe("4.8%");
    expect(view.fps).toBe("60.00 FPS");
    expect(view.bitrate).toBe("6.42 Mbps");
    expect(view.lan).toBe("http://192.168.46.78:8080/live");
    expect(view.onAir).toBe(true);
    expect(view.programFooter).toBe("Diffusion en cours...");
  });
});

describe("formatClock and gainLabel", () => {
  it("formats durations and fader gain", () => {
    expect(formatClock(0)).toBe("00:00:00");
    expect(formatClock(754)).toBe("00:12:34");
    expect(formatClock(-4)).toBe("00:00:00");
    expect(gainLabel(100)).toBe("0.0 dB");
    expect(gainLabel(90)).toBe("-6.0 dB");
    expect(gainLabel(80)).toBe("-12.0 dB");
    expect(gainLabel(100 - 8 / 0.6)).toBe("-8.0 dB");
    expect(gainLabel(100 - 10 / 0.6)).toBe("-10.0 dB");
  });
});
