import React, { useState } from "react";
import {
  Plus,
  MoreVertical,
  Eye,
  EyeOff,
  Lock,
  Unlock,
  GripVertical,
  Folder,
  Settings,
  Trash2,
  Video,
  Camera,
  QrCode,
  PlayCircle,
  Music,
  Diamond,
  BookOpen,
  Monitor,
  Smartphone,
} from "lucide-react";
import type { SceneSource } from "../../types/studio";

interface SourcesPanelProps {
  sources: SceneSource[];
  emphasis: boolean;
  onToggleVisibility: (id: string) => void;
  onToggleLock: (id: string) => void;
  onAddSource: (type: SceneSource["type"], name: string) => void;
  onRemoveSource: (id: string) => void;
  onPairMobile: () => void;
}

function SourceGlyph({ type, name }: { type: SceneSource["type"]; name: string }) {
  const common = { size: 13, strokeWidth: 1.75 };
  const key = name.toLowerCase();
  if (key.includes("logo")) return <Diamond {...common} />;
  if (key.includes("qr")) return <QrCode {...common} color="#6ee7a8" />;
  if (key.includes("média") || key.includes("media")) return <PlayCircle {...common} color="#3ddc6a" />;
  switch (type) {
    case "physical_camera":
      return <Video {...common} />;
    case "mobile_camera":
      return <Camera {...common} />;
    case "presentation_slide":
      return <QrCode {...common} color="#6ee7a8" />;
    case "media_file":
      return <PlayCircle {...common} color="#3ddc6a" />;
    case "audio_input":
      return <Music {...common} color="#7dd3fc" />;
    case "bible_verse":
      return <BookOpen {...common} color="#f5d90a" />;
    case "window_capture":
      return <Monitor {...common} />;
    default:
      return <Diamond {...common} />;
  }
}

const ADDABLES: { type: SceneSource["type"]; name: string; icon: React.ReactNode }[] = [
  { type: "physical_camera", name: "Caméra", icon: <Video size={12} /> },
  { type: "mobile_camera", name: "Caméra mobile", icon: <Smartphone size={12} /> },
  { type: "presentation_slide", name: "Diapositive", icon: <QrCode size={12} /> },
  { type: "media_file", name: "Média", icon: <PlayCircle size={12} /> },
  { type: "audio_input", name: "Audio", icon: <Music size={12} /> },
  { type: "bible_verse", name: "Verset", icon: <BookOpen size={12} /> },
  { type: "window_capture", name: "Capture fenêtre", icon: <Monitor size={12} /> },
];

export const SourcesPanel: React.FC<SourcesPanelProps> = ({
  sources,
  emphasis,
  onToggleVisibility,
  onToggleLock,
  onAddSource,
  onRemoveSource,
  onPairMobile,
}) => {
  const [menu, setMenu] = useState(false);
  const [selected, setSelected] = useState<string | null>(sources[0]?.id ?? null);

  return (
    <section className={`dock ${emphasis ? "emphasis" : ""}`} aria-label="Sources" style={{ position: "relative" }}>
      <header className="dock-head">
        <span>Sources</span>
        <span className="head-actions">
          <button type="button" className="icon-btn" title="Ajouter une source" onClick={() => setMenu((v) => !v)}>
            <Plus size={11} />
          </button>
          <button type="button" className="icon-btn" title="Plus" aria-label="Plus de sources">
            <MoreVertical size={11} />
          </button>
        </span>
      </header>
      {menu && (
        <div className="menu-pop add-menu">
          {ADDABLES.map((item) => (
            <button
              key={item.name}
              type="button"
              onClick={() => {
                setMenu(false);
                if (item.type === "mobile_camera") onPairMobile();
                onAddSource(item.type, item.name);
              }}
            >
              {item.icon} {item.name}
            </button>
          ))}
        </div>
      )}
      <div className="dock-body">
        {sources.length === 0 && (
          <div style={{ padding: 10, color: "#9aa3ab", fontSize: 11 }}>Aucune source dans cette scène.</div>
        )}
        {sources.map((src) => (
          <div
            key={src.id}
            className="source-row"
            onClick={() => setSelected(src.id)}
            style={{ background: selected === src.id ? "rgba(255,255,255,0.04)" : undefined, opacity: src.visible ? 1 : 0.45 }}
          >
            <button type="button" className={`ghost ${src.visible ? "on" : ""}`} title="Visibilité" onClick={() => onToggleVisibility(src.id)}>
              {src.visible ? <Eye size={12} /> : <EyeOff size={12} />}
            </button>
            <SourceGlyph type={src.type} name={src.name} />
            <span className="name">{src.name}</span>
            <button type="button" className="ghost" title={src.locked ? "Verrouillée" : "Déverrouiller"} onClick={() => onToggleLock(src.id)}>
              {src.locked ? <Lock size={11} /> : <Unlock size={11} />}
            </button>
            <span className="ghost" aria-hidden>
              <GripVertical size={11} />
            </span>
          </div>
        ))}
      </div>
      <footer className="dock-foot">
        <button type="button" className="icon-btn" title="Ajouter" onClick={() => setMenu(true)}>
          <Plus size={11} />
        </button>
        <button type="button" className="icon-btn" title="Dossier" aria-label="Dossier">
          <Folder size={11} />
        </button>
        <button type="button" className="icon-btn" title="Propriétés" aria-label="Propriétés">
          <Settings size={11} />
        </button>
        <button
          type="button"
          className="icon-btn"
          title="Supprimer"
          onClick={() => selected && onRemoveSource(selected)}
        >
          <Trash2 size={11} />
        </button>
      </footer>
    </section>
  );
};
