import React from "react";
import { Columns2, Settings, Maximize2, Scissors, Moon, Palette, Play, Radio } from "lucide-react";
import type { StudioScene } from "../../types/studio";

export type TransitionKind = "cut" | "fade-black" | "fade-color";

interface StudioPreviewProgramProps {
  previewScene: StudioScene;
  programScene: StudioScene;
  studioMode: boolean;
  transition: TransitionKind;
  durationMs: number;
  onDuration: (ms: number) => void;
  onSelectTransition: (kind: TransitionKind) => void;
  onApply: (kind?: TransitionKind) => void;
  demo: boolean;
  onAir: boolean;
  programFooter: string;
  programClock: string;
  blackout: boolean;
  colorFade: boolean;
}

const Stage: React.FC<{ demo: boolean; label: string }> = ({ demo, label }) => {
  if (!demo) {
    return (
      <div className="monitor-empty">
        <strong>Aucun signal</strong>
        <span>{label}</span>
      </div>
    );
  }
  return (
    <div className="demo-stage" aria-hidden>
      <div className="cross" />
      <div className="floor" />
      <div className="seats" />
      <div className="demo-watermark">DÉMO</div>
    </div>
  );
};

export const StudioPreviewProgram: React.FC<StudioPreviewProgramProps> = ({
  previewScene,
  programScene,
  studioMode,
  transition,
  durationMs,
  onDuration,
  onSelectTransition,
  onApply,
  demo,
  onAir,
  programFooter,
  programClock,
  blackout,
  colorFade,
}) => {
  const visibleNames = previewScene.sources.filter((s) => s.visible).map((s) => s.name);
  const previewSource = visibleNames.length
    ? `${visibleNames[0]} + ${visibleNames.some((n) => /m[ée]dia/i.test(n)) ? "Média" : visibleNames[1] ?? "Média"}`
    : "Aucune source";

  return (
    <section className={`monitors ${studioMode ? "" : "single"}`}>
      {studioMode && (
        <div className="monitor preview">
          <div className="monitor-stage">
            <Stage demo={demo} label="Aperçu sans flux vidéo" />
          </div>
          <div className="mon-bar" style={{ color: "#2fe06a" }}>
            <span>[ APERÇU (PREVIEW) ]</span>
            <span className="mon-tools">
              <button type="button" title="Disposition" aria-label="Disposition">
                <Columns2 size={12} />
              </button>
              <button type="button" title="Réglages aperçu" aria-label="Réglages aperçu">
                <Settings size={12} />
              </button>
              <button type="button" title="Plein cadre" aria-label="Plein cadre">
                <Maximize2 size={12} />
              </button>
            </span>
          </div>
          <div className="mon-caption">
            <div className="t">◉ Scène : {previewScene.name}</div>
            <div className="s">Source : {previewSource}</div>
          </div>
        </div>
      )}

      {studioMode && (
        <div className="transition">
          <h2>» TRANSITION</h2>
          <button
            type="button"
            className={`t-btn cut ${transition === "cut" ? "active" : ""}`}
            onClick={() => onApply("cut")}
          >
            <Scissors size={13} /> Couper
          </button>
          <button
            type="button"
            className={`t-btn ${transition === "fade-black" ? "active" : ""}`}
            onClick={() => onSelectTransition("fade-black")}
          >
            <Moon size={13} /> Fondu Noir
          </button>
          <button
            type="button"
            className={`t-btn ${transition === "fade-color" ? "active" : ""}`}
            onClick={() => onSelectTransition("fade-color")}
          >
            <Palette size={13} /> Fondu Couleur
          </button>
          <div className="duration-row">
            <span>Durée :</span>
            <input
              type="range"
              min={100}
              max={1000}
              step={50}
              value={durationMs}
              aria-label="Durée de transition"
              onChange={(e) => onDuration(Number(e.target.value))}
            />
            <b>{durationMs} ms</b>
          </div>
          <button type="button" className="t-btn" onClick={() => onApply()}>
            <Play size={12} /> Appliquer
          </button>
        </div>
      )}

      <div className="monitor program">
        <div className="monitor-stage">
          {blackout ? (
            <div className="monitor-empty">
              <strong>Fondu noir</strong>
              <span>Programme coupé</span>
            </div>
          ) : colorFade ? (
            <div className="monitor-empty" style={{ background: "#1a2744" }}>
              <strong>Fondu couleur</strong>
              <span>{programScene.name}</span>
            </div>
          ) : (
            <Stage demo={demo} label="Programme sans flux vidéo" />
          )}
        </div>
        <div className="mon-bar" style={{ color: "#ff3b3b" }}>
          <span>● PROGRAMME (ON AIR)</span>
          <span className={`onair-pill ${onAir ? "" : "idle"}`}>
            <Radio size={10} /> {onAir ? "ON AIR" : "HORS ANTENNE"}
          </span>
        </div>
        <div className="pgm-footer">
          <span>{programFooter}</span>
          <span className="clock">{programClock}</span>
        </div>
      </div>
    </section>
  );
};
