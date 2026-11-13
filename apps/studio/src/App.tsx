import React, { useEffect, useMemo, useState } from "react";
import { createPortal } from "react-dom";
import { LeftRail, StudioNav } from "./components/layout/LeftRail";
import { TitleBar } from "./components/layout/TitleBar";
import { StatusBar } from "./components/layout/StatusBar";
import { StudioPreviewProgram, TransitionKind } from "./components/studio/StudioPreviewProgram";
import { ScenesPanel } from "./components/studio/ScenesPanel";
import { SourcesPanel } from "./components/studio/SourcesPanel";
import { AudioMixerPanel } from "./components/studio/AudioMixerPanel";
import { ControlsPanel } from "./components/studio/ControlsPanel";
import { CameraPairingModal } from "./components/modals/CameraPairingModal";
import { TreasuryWithdrawalModal } from "./components/modals/TreasuryWithdrawalModal";
import { SettingsModal } from "./components/modals/SettingsModal";
import { PostProductionModal } from "./components/modals/PostProductionModal";
import { AudioChannel, SceneId, SceneSource, StudioScene } from "./types/studio";
import { viewStatus } from "./lib/studioMetrics";

const DB = (db: number) => 100 + db / 0.6;

function initialScenes(): StudioScene[] {
  return [
    {
      id: "sc-main",
      name: "Culte Principal",
      sources: [
        { id: "cam1", name: "Caméra 1", type: "physical_camera", visible: true, locked: true },
        { id: "cam2", name: "Caméra 2", type: "mobile_camera", visible: true, locked: true },
        { id: "qr", name: "QR Code", type: "presentation_slide", visible: true, locked: false },
        { id: "media", name: "Media Pastor", type: "media_file", visible: true, locked: false },
        { id: "music", name: "Fond Musical", type: "audio_input", visible: true, locked: false },
        { id: "logo", name: "Logo ECC", type: "presentation_slide", visible: true, locked: true },
      ],
    },
    {
      id: "sc-louange",
      name: "Louange",
      sources: [
        { id: "lou-cam2", name: "Caméra 2", type: "mobile_camera", visible: true, locked: false },
        { id: "lou-cam1", name: "Caméra 1", type: "physical_camera", visible: true, locked: true },
        { id: "lou-mic", name: "Micros Chœur", type: "audio_input", visible: true, locked: true },
      ],
    },
    {
      id: "sc-teach",
      name: "Enseignement",
      sources: [
        { id: "ens-cam", name: "Caméra 1", type: "physical_camera", visible: true, locked: true },
        { id: "ens-bible", name: "Verset", type: "bible_verse", visible: true, locked: false },
        { id: "ens-mic", name: "Micro Pasteur", type: "audio_input", visible: true, locked: true },
      ],
    },
    {
      id: "sc-prayer",
      name: "Prière",
      sources: [
        { id: "pri-cam", name: "Caméra 1", type: "physical_camera", visible: true, locked: true },
        { id: "pri-mus", name: "Fond Musical", type: "audio_input", visible: true, locked: false },
      ],
    },
    {
      id: "sc-end",
      name: "Fin",
      sources: [
        { id: "fin-media", name: "Media Pastor", type: "media_file", visible: true, locked: false },
        { id: "fin-qr", name: "QR Code", type: "presentation_slide", visible: true, locked: false },
        { id: "fin-mus", name: "Fond Musical", type: "audio_input", visible: true, locked: false },
      ],
    },
  ];
}

function initialChannels(): AudioChannel[] {
  return [
    { id: "master", name: "Master (PGM)", volume: DB(0), muted: false, solo: false, peakLevel: 0, hasFilters: true },
    { id: "pastor", name: "Micro Pasteur", volume: DB(-6), muted: true, solo: false, peakLevel: 0, hasFilters: true },
    { id: "choir", name: "Chœur / Orchestre", volume: DB(-12), muted: false, solo: false, peakLevel: 0, hasFilters: true },
    { id: "ecc-cam", name: "ECC Caméra 1", volume: DB(-8), muted: true, solo: false, peakLevel: 0, hasFilters: false },
    { id: "media", name: "Médias & Vidéo", volume: DB(-10), muted: false, solo: false, peakLevel: 0, hasFilters: false },
  ];
}

function readDemoFlag(): boolean {
  if (typeof window === "undefined") return false;
  return new URLSearchParams(window.location.search).get("demo") === "1";
}

export default function App() {
  const [nav, setNav] = useState<StudioNav>("accueil");
  const [demo, setDemo] = useState(readDemoFlag);
  const [screenLabel, setScreenLabel] = useState("Écran principal");
  const [studioMode, setStudioMode] = useState(true);
  const [isLive, setIsLive] = useState(false);
  const [isRecording, setIsRecording] = useState(false);
  const [liveSeconds, setLiveSeconds] = useState(0);
  const [recordSeconds, setRecordSeconds] = useState(0);
  const [note, setNote] = useState<string | null>(null);
  const [menu, setMenu] = useState<null | "outils" | "playlists">(null);

  const [transition, setTransition] = useState<TransitionKind>("cut");
  const [durationMs, setDurationMs] = useState(400);
  const [blackout, setBlackout] = useState(false);
  const [colorFade, setColorFade] = useState(false);

  const [scenes, setScenes] = useState<StudioScene[]>(initialScenes);
  const [previewSceneId, setPreviewSceneId] = useState<SceneId>("sc-main");
  const [programSceneId, setProgramSceneId] = useState<SceneId>("sc-main");
  const [channels, setChannels] = useState<AudioChannel[]>(initialChannels);

  const [isPairingOpen, setIsPairingOpen] = useState(false);
  const [isTreasuryOpen, setIsTreasuryOpen] = useState(false);
  const [isSettingsOpen, setIsSettingsOpen] = useState(false);
  const [isPostProdOpen, setIsPostProdOpen] = useState(false);

  useEffect(() => {
    if (!isLive && !isRecording) return;
    const id = window.setInterval(() => {
      if (isLive) setLiveSeconds((s) => s + 1);
      if (isRecording) setRecordSeconds((s) => s + 1);
    }, 1000);
    return () => window.clearInterval(id);
  }, [isLive, isRecording]);

  const previewScene = scenes.find((s) => s.id === previewSceneId) ?? scenes[0];
  const programScene = scenes.find((s) => s.id === programSceneId) ?? scenes[0];
  const master = channels.find((c) => c.id === "master");

  const status = useMemo(
    () =>
      viewStatus({
        demo,
        isLive,
        isRecording,
        liveSeconds,
        recordSeconds,
        cpuPercent: null,
        videoFps: 0,
        bitrateMbps: 0,
        lanUrl: null,
      }),
    [demo, isLive, isRecording, liveSeconds, recordSeconds]
  );

  const applyTransition = (kind: TransitionKind = transition) => {
    setTransition(kind);
    if (kind === "cut") {
      setProgramSceneId(previewSceneId);
      setBlackout(false);
      setColorFade(false);
      return;
    }
    window.setTimeout(() => {
      setProgramSceneId(previewSceneId);
      setBlackout(kind === "fade-black");
      setColorFade(kind === "fade-color");
    }, durationMs);
  };

  const updatePreviewSources = (map: (src: SceneSource) => SceneSource) => {
    setScenes((prev) =>
      prev.map((sc) => (sc.id === previewSceneId ? { ...sc, sources: sc.sources.map(map) } : sc))
    );
  };

  const selectNav = (id: StudioNav) => {
    setNav(id);
    setMenu(null);
    if (id === "parametres") setIsSettingsOpen(true);
    if (id === "outils") setMenu("outils");
    if (id === "playlists") setMenu("playlists");
    if (id === "diffusion") setNote(null);
  };

  const refuseStream = () => {
    if (demo) {
      setIsLive((v) => !v);
      if (isLive) setLiveSeconds(0);
      setNote("Mode démo : le voyant suit l'exemple, aucune sortie n'est ouverte.");
      return;
    }
    setIsLive(false);
    setNote("Aucune sortie de diffusion connectée. Le statut reste hors antenne.");
  };

  const refuseRecord = () => {
    if (demo) {
      setIsRecording((v) => !v);
      if (isRecording) setRecordSeconds(0);
      setNote("Mode démo : aucun fichier n'est écrit.");
      return;
    }
    setIsRecording(false);
    setNote("Aucun enregistrement démarré : pas de périphérique de capture.");
  };

  return (
    <div className="studio-root">
      <LeftRail active={nav} onSelect={selectNav} />
      <div className="workspace">
        <TitleBar
          active={nav}
          onSelect={selectNav}
          screenLabel={screenLabel}
          onScreen={setScreenLabel}
          recording={status.recOn}
          studioMode={studioMode}
          onToggleStudioMode={() => setStudioMode((v) => !v)}
          demo={demo}
        />

        <StudioPreviewProgram
          previewScene={previewScene}
          programScene={programScene}
          studioMode={studioMode}
          transition={transition}
          durationMs={durationMs}
          onDuration={setDurationMs}
          onSelectTransition={setTransition}
          onApply={(kind) => applyTransition(kind)}
          demo={demo}
          onAir={status.onAir}
          programFooter={demo ? `DÉMO · ${status.programFooter}` : status.programFooter}
          programClock={status.programClock}
          blackout={blackout}
          colorFade={colorFade}
        />

        <div className="docks">
          <ScenesPanel
            scenes={scenes}
            selectedSceneId={previewSceneId}
            emphasis={nav === "scenes"}
            onSelectScene={setPreviewSceneId}
            onAddScene={() => {
              const id = `sc-${Date.now()}`;
              setScenes((prev) => [...prev, { id, name: `Scène ${prev.length + 1}`, sources: [] }]);
              setPreviewSceneId(id);
            }}
            onDeleteScene={(id) => {
              setScenes((prev) => prev.filter((s) => s.id !== id));
              if (previewSceneId === id) setPreviewSceneId(scenes.find((s) => s.id !== id)?.id ?? "");
            }}
            onMove={(id, dir) => {
              setScenes((prev) => {
                const index = prev.findIndex((s) => s.id === id);
                const next = index + dir;
                if (index < 0 || next < 0 || next >= prev.length) return prev;
                const copy = prev.slice();
                const [item] = copy.splice(index, 1);
                copy.splice(next, 0, item);
                return copy;
              });
            }}
          />

          <SourcesPanel
            sources={previewScene.sources}
            emphasis={nav === "medias"}
            onToggleVisibility={(id) =>
              updatePreviewSources((src) => (src.id === id ? { ...src, visible: !src.visible } : src))
            }
            onToggleLock={(id) =>
              updatePreviewSources((src) => (src.id === id ? { ...src, locked: !src.locked } : src))
            }
            onAddSource={(type, name) => {
              const id = `src-${Date.now()}`;
              setScenes((prev) =>
                prev.map((sc) =>
                  sc.id === previewSceneId
                    ? { ...sc, sources: [...sc.sources, { id, name, type, visible: true, locked: false }] }
                    : sc
                )
              );
            }}
            onRemoveSource={(id) => {
              setScenes((prev) =>
                prev.map((sc) =>
                  sc.id === previewSceneId ? { ...sc, sources: sc.sources.filter((s) => s.id !== id) } : sc
                )
              );
            }}
            onPairMobile={() => setIsPairingOpen(true)}
          />

          <AudioMixerPanel
            channels={channels}
            demo={demo}
            onVolumeChange={(id, vol) =>
              setChannels((prev) => prev.map((c) => (c.id === id ? { ...c, volume: vol } : c)))
            }
            onToggleMute={(id) =>
              setChannels((prev) => prev.map((c) => (c.id === id ? { ...c, muted: !c.muted } : c)))
            }
          />

          <ControlsPanel
            emphasis={nav === "diffusion" || nav === "enregistrement"}
            isLive={status.liveOn}
            isRecording={status.recOn}
            studioMode={studioMode}
            note={note}
            onToggleLive={refuseStream}
            onToggleRecording={refuseRecord}
            onToggleStudioMode={() => setStudioMode((v) => !v)}
            onSnapshot={() =>
              setNote(
                demo
                  ? "Mode démo : aucun instantané n'est enregistré."
                  : "Aucun signal à capturer."
              )
            }
            onQuickSettings={() => setIsSettingsOpen(true)}
          />
        </div>

        <StatusBar
          status={status}
          masterVolume={master?.volume ?? 100}
          onMasterVolume={(vol) =>
            setChannels((prev) => prev.map((c) => (c.id === "master" ? { ...c, volume: vol } : c)))
          }
        />
      </div>

      {createPortal(
        <>
          {menu && (
            <div className="menu-pop" style={{ position: "fixed", left: 70, top: menu === "outils" ? 300 : 210 }}>
              {menu === "outils" ? (
                <>
                  <button type="button" onClick={() => { setMenu(null); setIsPairingOpen(true); }}>
                    Appairer une caméra
                  </button>
                  <button type="button" onClick={() => { setMenu(null); setIsPostProdOpen(true); }}>
                    Postproduction du culte
                  </button>
                  <button type="button" onClick={() => { setMenu(null); setIsTreasuryOpen(true); }}>
                    Trésorerie et dons
                  </button>
                  <button
                    type="button"
                    onClick={() => {
                      setDemo((v) => !v);
                      setMenu(null);
                      setIsLive(false);
                      setIsRecording(false);
                    }}
                  >
                    {demo ? "Quitter le mode démo" : "Activer le mode démo (données d'exemple)"}
                  </button>
                </>
              ) : (
                scenes.map((scene, index) => (
                  <button key={scene.id} type="button" onClick={() => { setPreviewSceneId(scene.id); setMenu(null); }}>
                    {index + 1}. {scene.name}
                  </button>
                ))
              )}
            </div>
          )}
          <CameraPairingModal isOpen={isPairingOpen} onClose={() => setIsPairingOpen(false)} />
          <TreasuryWithdrawalModal isOpen={isTreasuryOpen} onClose={() => setIsTreasuryOpen(false)} />
          <SettingsModal isOpen={isSettingsOpen} onClose={() => setIsSettingsOpen(false)} />
          <PostProductionModal isOpen={isPostProdOpen} onClose={() => setIsPostProdOpen(false)} />
        </>,
        document.body
      )}
    </div>
  );
}
