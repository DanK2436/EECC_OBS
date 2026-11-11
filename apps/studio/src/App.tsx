import React, { useState } from "react";
import { TitleBar } from "./components/layout/TitleBar";
import { StatusBar } from "./components/layout/StatusBar";
import { StudioPreviewProgram } from "./components/studio/StudioPreviewProgram";
import { ScenesPanel } from "./components/studio/ScenesPanel";
import { SourcesPanel } from "./components/studio/SourcesPanel";
import { AudioMixerPanel } from "./components/studio/AudioMixerPanel";
import { ControlsPanel } from "./components/studio/ControlsPanel";
import { CameraPairingModal } from "./components/modals/CameraPairingModal";
import { TreasuryWithdrawalModal } from "./components/modals/TreasuryWithdrawalModal";
import { SettingsModal } from "./components/modals/SettingsModal";
import { PostProductionModal } from "./components/modals/PostProductionModal";
import { StudioScene, SceneId, AudioChannel } from "./types/studio";

export default function App() {
  // États de Diffusion et Enregistrement
  const [isLive, setIsLive] = useState(false);
  const [isRecording, setIsRecording] = useState(false);
  const [studioMode, setStudioMode] = useState(true);
  const [transitionDurationMs, setTransitionDurationMs] = useState(300);

  // Scènes prédéfinies
  const [scenes, setScenes] = useState<StudioScene[]>([
    {
      id: "sc-intro",
      name: "Accueil & Décompte",
      sources: [
        { id: "src-vid", name: "Vidéo Intro EECC", type: "media_file", visible: true, locked: true },
        { id: "src-mus", name: "Musique d'accueil", type: "audio_input", visible: true, locked: true },
      ],
    },
    {
      id: "sc-speaker",
      name: "Orateur Principal",
      sources: [
        { id: "src-cam1", name: "Caméra 1 (Pupitre)", type: "physical_camera", visible: true, locked: true },
        { id: "src-mic1", name: "Micro Cravate Pasteur", type: "audio_input", visible: true, locked: true },
        { id: "src-slide", name: "Diapositive Titre", type: "presentation_slide", visible: true, locked: false },
      ],
    },
    {
      id: "sc-louange",
      name: "Chœur & Louange",
      sources: [
        {
          id: "src-mob1",
          name: "EECC Caméra (Smartphone 1)",
          type: "mobile_camera",
          visible: true,
          locked: false,
          telemetry: { battery: 85, bitrate: 4500, fps: 30, ip: "192.168.1.102" },
        },
        { id: "src-choir", name: "Micros Chœur & Orchestre", type: "audio_input", visible: true, locked: true },
      ],
    },
    {
      id: "sc-wide",
      name: "Plan Large Assemblée",
      sources: [
        { id: "src-wide-cam", name: "Caméra Fond de Temple", type: "physical_camera", visible: true, locked: true },
      ],
    },
    {
      id: "sc-bible",
      name: "Bible & Verset Plein Écran",
      sources: [
        { id: "src-bible-txt", name: "Verset : Jean 3:16 (LSG)", type: "bible_verse", visible: true, locked: true },
        { id: "src-back-cam", name: "Arrière-plan Flou", type: "physical_camera", visible: true, locked: true },
      ],
    },
  ]);

  const [previewSceneId, setPreviewSceneId] = useState<SceneId>("sc-louange");
  const [programSceneId, setProgramSceneId] = useState<SceneId>("sc-speaker");

  // Mixeur Audio
  const [channels, setChannels] = useState<AudioChannel[]>([
    { id: "mic-1", name: "Micro Orateur", volume: 85, muted: false, solo: false, peakLevel: 0.72, hasFilters: true },
    { id: "mic-2", name: "Chœur / Louange", volume: 75, muted: false, solo: false, peakLevel: 0.65, hasFilters: true },
    { id: "cam-mob", name: "Caméra Smartphone", volume: 60, muted: false, solo: false, peakLevel: 0.40, hasFilters: false },
    { id: "pc-audio", name: "Audio Ordinateur", volume: 50, muted: false, solo: false, peakLevel: 0.30, hasFilters: false },
    { id: "master", name: "MASTER SORTIE", volume: 90, muted: false, solo: false, peakLevel: 0.82, hasFilters: true },
  ]);

  // Boîtes de dialogue
  const [isPairingOpen, setIsPairingOpen] = useState(false);
  const [isTreasuryOpen, setIsTreasuryOpen] = useState(false);
  const [isSettingsOpen, setIsSettingsOpen] = useState(false);
  const [isPostProdOpen, setIsPostProdOpen] = useState(false);

  const previewScene = scenes.find((s) => s.id === previewSceneId) || scenes[0];
  const programScene = scenes.find((s) => s.id === programSceneId) || scenes[1];

  // Exécution de la transition Aperçu -> Programme
  const handleExecuteTransition = () => {
    const temp = programSceneId;
    setProgramSceneId(previewSceneId);
    setPreviewSceneId(temp);
  };

  const handleToggleSourceVisibility = (sourceId: string) => {
    setScenes((prev) =>
      prev.map((sc) => {
        if (sc.id === previewSceneId) {
          return {
            ...sc,
            sources: sc.sources.map((src) =>
              src.id === sourceId ? { ...src, visible: !src.visible } : src
            ),
          };
        }
        return sc;
      })
    );
  };

  const handleToggleSourceLock = (sourceId: string) => {
    setScenes((prev) =>
      prev.map((sc) => {
        if (sc.id === previewSceneId) {
          return {
            ...sc,
            sources: sc.sources.map((src) =>
              src.id === sourceId ? { ...src, locked: !src.locked } : src
            ),
          };
        }
        return sc;
      })
    );
  };

  return (
    <div className="h-full flex flex-col bg-background overflow-hidden">
      {/* 1. Barre de Titre et Menus */}
      <TitleBar
        projectName="Culte Dominical — EECC Matadi"
        isSaved={true}
        onOpenPairing={() => setIsPairingOpen(true)}
        onOpenTreasury={() => setIsTreasuryOpen(true)}
        onOpenSettings={() => setIsSettingsOpen(true)}
        onOpenPostProd={() => setIsPostProdOpen(true)}
      />

      {/* 2. Écran Central Moniteurs (Aperçu / Programme) */}
      <StudioPreviewProgram
        previewScene={previewScene}
        programScene={programScene}
        studioMode={studioMode}
        transitionDurationMs={transitionDurationMs}
        onSetTransitionDuration={setTransitionDurationMs}
        onExecuteTransition={handleExecuteTransition}
      />

      {/* 3. Dock Inférieur : Scènes, Sources, Mixeur Audio, Contrôles */}
      <div className="h-56 bg-surface border-t border-border flex divide-x divide-border">
        {/* Panneau Scènes */}
        <ScenesPanel
          scenes={scenes}
          selectedSceneId={previewSceneId}
          onSelectScene={setPreviewSceneId}
          onAddScene={() => {
            const newId = `sc-${Date.now()}`;
            setScenes([...scenes, { id: newId, name: `Nouvelle Scène ${scenes.length + 1}`, sources: [] }]);
          }}
          onDeleteScene={(id) => setScenes(scenes.filter((s) => s.id !== id))}
        />

        {/* Panneau Sources */}
        <SourcesPanel
          sources={previewScene.sources}
          onToggleVisibility={handleToggleSourceVisibility}
          onToggleLock={handleToggleSourceLock}
          onAddSource={() => {}}
          onPairMobileCamera={() => setIsPairingOpen(true)}
        />

        {/* Panneau Mixeur Audio DSP */}
        <AudioMixerPanel
          channels={channels}
          onVolumeChange={(id, vol) =>
            setChannels((prev) => prev.map((c) => (c.id === id ? { ...c, volume: vol } : c)))
          }
          onToggleMute={(id) =>
            setChannels((prev) => prev.map((c) => (c.id === id ? { ...c, muted: !c.muted } : c)))
          }
          onOpenDspFilters={() => {}}
        />

        {/* Panneau Contrôles Principaux */}
        <ControlsPanel
          isLive={isLive}
          isRecording={isRecording}
          studioMode={studioMode}
          onToggleLive={() => setIsLive(!isLive)}
          onToggleRecording={() => setIsRecording(!isRecording)}
          onToggleStudioMode={() => setStudioMode(!studioMode)}
          onOpenTreasury={() => setIsTreasuryOpen(true)}
          onOpenPostProd={() => setIsPostProdOpen(true)}
          onOpenSettings={() => setIsSettingsOpen(true)}
        />
      </div>

      {/* 4. Barre d'état inférieure */}
      <StatusBar
        isLive={isLive}
        isRecording={isRecording}
        liveDuration="01:24:18"
        recordDuration="01:24:18"
        cpuUsage={18}
        memoryUsageMb={640}
        fps={60}
        droppedFrames={0}
        bitrateKbps={4500}
      />

      {/* 5. Fenêtres Modales */}
      <CameraPairingModal
        isOpen={isPairingOpen}
        onClose={() => setIsPairingOpen(false)}
        localIp="192.168.1.50"
        port={8554}
      />

      <TreasuryWithdrawalModal
        isOpen={isTreasuryOpen}
        onClose={() => setIsTreasuryOpen(false)}
      />

      <SettingsModal
        isOpen={isSettingsOpen}
        onClose={() => setIsSettingsOpen(false)}
      />

      <PostProductionModal
        isOpen={isPostProdOpen}
        onClose={() => setIsPostProdOpen(false)}
      />
    </div>
  );
}
