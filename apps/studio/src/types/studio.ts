export type SceneId = string;

export interface SceneSource {
  id: string;
  name: string;
  type: 'physical_camera' | 'mobile_camera' | 'audio_input' | 'window_capture' | 'media_file' | 'presentation_slide' | 'bible_verse';
  visible: boolean;
  locked: boolean;
  telemetry?: {
    battery?: number;
    bitrate?: number;
    fps?: number;
    ip?: string;
  };
}

export interface StudioScene {
  id: SceneId;
  name: string;
  sources: SceneSource[];
}

export interface AudioChannel {
  id: string;
  name: string;
  volume: number; // 0 to 100
  muted: boolean;
  solo: boolean;
  peakLevel: number; // 0 to 1.0 (pour VU-mètre)
  hasFilters: boolean;
}

export interface StreamDestination {
  id: string;
  name: string;
  platform: 'youtube' | 'facebook' | 'tiktok' | 'custom';
  rtmpUrl: string;
  streamKey: string;
  enabled: boolean;
  connected: boolean;
  bitrateKbps: number;
}
