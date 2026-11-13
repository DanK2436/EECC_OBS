import React from "react";
import { Plus, MoreVertical, ChevronUp, ChevronDown, Folder, Trash2 } from "lucide-react";
import type { SceneId, StudioScene } from "../../types/studio";

interface ScenesPanelProps {
  scenes: StudioScene[];
  selectedSceneId: SceneId;
  emphasis: boolean;
  onSelectScene: (id: SceneId) => void;
  onAddScene: () => void;
  onDeleteScene: (id: SceneId) => void;
  onMove: (id: SceneId, dir: -1 | 1) => void;
}

export const ScenesPanel: React.FC<ScenesPanelProps> = ({
  scenes,
  selectedSceneId,
  emphasis,
  onSelectScene,
  onAddScene,
  onDeleteScene,
  onMove,
}) => {
  return (
    <section className={`dock ${emphasis ? "emphasis" : ""}`} aria-label="Scènes">
      <header className="dock-head">
        <span>Scènes</span>
        <span className="head-actions">
          <button type="button" className="icon-btn" title="Ajouter une scène" onClick={onAddScene}>
            <Plus size={11} />
          </button>
          <button type="button" className="icon-btn" title="Plus" aria-label="Plus de scènes">
            <MoreVertical size={11} />
          </button>
        </span>
      </header>
      <div className="dock-body">
        {scenes.map((scene) => (
          <button
            key={scene.id}
            type="button"
            className={`scene-row ${scene.id === selectedSceneId ? "selected" : ""}`}
            onClick={() => onSelectScene(scene.id)}
          >
            <span className="thumb" aria-hidden />
            <span>{scene.name}</span>
          </button>
        ))}
      </div>
      <footer className="dock-foot">
        <button type="button" className="icon-btn" title="Monter" onClick={() => onMove(selectedSceneId, -1)}>
          <ChevronUp size={12} />
        </button>
        <button type="button" className="icon-btn" title="Descendre" onClick={() => onMove(selectedSceneId, 1)}>
          <ChevronDown size={12} />
        </button>
        <button type="button" className="icon-btn" title="Groupe" aria-label="Groupe">
          <Folder size={11} />
        </button>
        <button
          type="button"
          className="icon-btn"
          title="Supprimer"
          onClick={() => scenes.length > 1 && onDeleteScene(selectedSceneId)}
        >
          <Trash2 size={11} />
        </button>
      </footer>
    </section>
  );
};
