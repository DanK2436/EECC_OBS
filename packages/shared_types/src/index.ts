// ==============================================================================
// EECC Ecosystem - Types TypeScript Partagés
// ==============================================================================

export type AppRole = 'visitor' | 'fidele' | 'editor' | 'producer' | 'treasurer' | 'admin';

export type EventStatus = 'draft' | 'scheduled' | 'live' | 'ended' | 'archived';

export type MediaType =
  | 'audio_raw'
  | 'podcast'
  | 'video_cut'
  | 'document_pdf'
  | 'document_docx'
  | 'document_txt';

export type MediaStatus = 'uploading' | 'processing' | 'ready' | 'failed' | 'archived';

export type ReplayPlatform = 'youtube' | 'facebook' | 'tiktok' | 'vimeo' | 'other';

export type ReplayAvailability = 'available' | 'unverified' | 'unavailable' | 'deleted';

export type DonationStatus = 'pending' | 'confirmed' | 'failed' | 'refunded';

export type PaymentMethod = 'mobile_money' | 'card' | 'bank_transfer' | 'cash' | 'other';

export type WithdrawalStatus =
  | 'draft'
  | 'pending_otp'
  | 'otp_verified'
  | 'otp_expired'
  | 'cancelled'
  | 'locked'
  | 'processing'
  | 'completed'
  | 'failed'
  | 'pending';

export interface UserProfile {
  id: string;
  display_name: string | null;
  email: string;
  phone?: string | null;
  avatar_url?: string | null;
  locale: string;
  created_at: string;
  updated_at: string;
}

export interface ChurchLocation {
  id: string;
  name: string;
  description?: string | null;
  address: string;
  city: string;
  country: string;
  phone?: string | null;
  email?: string | null;
  schedules: Array<{ day: string; time: string; label: string }>;
  latitude?: number | null;
  longitude?: number | null;
  is_main_campus: boolean;
}

export interface ChurchEvent {
  id: string;
  church_id?: string | null;
  title: string;
  description?: string | null;
  start_time: string;
  end_time?: string | null;
  event_type: string;
  status: EventStatus;
  live_stream_url?: string | null;
}

export interface PlatformReplay {
  id: string;
  event_id: string;
  platform: ReplayPlatform;
  external_id?: string | null;
  url: string;
  availability_status: ReplayAvailability;
  last_checked_at?: string | null;
}

export interface Donation {
  id: string;
  reference: string;
  donor_id?: string | null;
  donor_name?: string | null;
  donor_email?: string | null;
  donor_phone?: string | null;
  amount: number;
  currency: string;
  method: PaymentMethod;
  provider: string;
  provider_transaction_id?: string | null;
  provider_fee?: number;
  status: DonationStatus;
  created_at: string;
}

export interface WithdrawalRequest {
  id: string;
  reference: string;
  requested_by: string;
  amount_gross: number;
  fee: number;
  amount_net: number;
  currency: string;
  destination_account: string;
  destination_provider: string;
  status: WithdrawalStatus;
  otp_expires_at?: string | null;
  created_at: string;
}
