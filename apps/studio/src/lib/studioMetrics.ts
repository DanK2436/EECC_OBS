/** Affichage des statuts régie. Les chiffres d'exemple n'existent qu'en mode démo explicite. */

export interface StatusInput {
  demo: boolean;
  isLive: boolean;
  isRecording: boolean;
  liveSeconds: number;
  recordSeconds: number;
  /** null quand le CPU du processus n'est pas mesurable. */
  cpuPercent: number | null;
  videoFps: number;
  bitrateMbps: number;
  /** URL réelle du serveur local, ou null si aucune interface n'est connue. */
  lanUrl: string | null;
}

export interface StatusView {
  liveOn: boolean;
  recOn: boolean;
  liveTime: string;
  recTime: string;
  cpu: string;
  fps: string;
  bitrate: string;
  lan: string;
  onAir: boolean;
  programFooter: string;
  programClock: string;
  exampleData: boolean;
}

export function formatClock(totalSeconds: number): string {
  const safe = Number.isFinite(totalSeconds) ? Math.max(0, Math.floor(totalSeconds)) : 0;
  const h = Math.floor(safe / 3600);
  const m = Math.floor((safe % 3600) / 60);
  const s = safe % 60;
  return [h, m, s].map((n) => String(n).padStart(2, "0")).join(":");
}

/** Gain affiché sous le fader. 100 = 0,0 dB, chaque cran vaut −0,6 dB. */
export function gainLabel(volume: number): string {
  const db = Math.round((volume - 100) * 0.6 * 10) / 10;
  const sign = db > 0 ? "+" : "";
  return `${sign}${db.toFixed(1)} dB`;
}

const EXAMPLE: StatusView = {
  liveOn: true,
  recOn: false,
  liveTime: "00:12:34",
  recTime: "00:00:00",
  cpu: "4.8%",
  fps: "60.00 FPS",
  bitrate: "6.42 Mbps",
  lan: "http://192.168.46.78:8080/live",
  onAir: true,
  programFooter: "Diffusion en cours...",
  programClock: "00:12:34",
  exampleData: true,
};

export function viewStatus(input: StatusInput): StatusView {
  if (input.demo) return EXAMPLE;

  const liveOn = input.isLive;
  const recOn = input.isRecording;
  const bitrate = liveOn ? Math.max(0, input.bitrateMbps) : 0;
  const fps = Math.max(0, input.videoFps);

  return {
    liveOn,
    recOn,
    liveTime: liveOn ? formatClock(input.liveSeconds) : "00:00:00",
    recTime: recOn ? formatClock(input.recordSeconds) : "00:00:00",
    cpu: input.cpuPercent == null || !Number.isFinite(input.cpuPercent) ? "—" : `${input.cpuPercent.toFixed(1)}%`,
    fps: `${fps.toFixed(2)} FPS`,
    bitrate: `${bitrate.toFixed(2)} Mbps`,
    lan: input.lanUrl && input.lanUrl.trim() ? input.lanUrl : "non détecté",
    onAir: liveOn,
    programFooter: liveOn ? "Diffusion en cours..." : "En attente",
    programClock: liveOn ? formatClock(input.liveSeconds) : "",
    exampleData: false,
  };
}

/** Niveaux de vu-mètres d'exemple, uniquement pour le mode démo. 0 = silence. */
export const DEMO_METER_LEVELS: Record<string, number> = {
  master: 0.72,
  pastor: 0.9,
  choir: 0.55,
  "ecc-cam": 0.78,
  media: 0.6,
};
