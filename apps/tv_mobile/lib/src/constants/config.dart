class EeccConfig {
  static const String appName = 'EECC TV';
  static const String appVersion = '1.0.0';

  // Supabase Configuration Réelle
  static const String supabaseUrl = String.fromEnvironment(
    'SUPABASE_URL',
    defaultValue: 'https://rwojdiomftillhsamjal.supabase.co',
  );

  static const String supabaseAnonKey = String.fromEnvironment(
    'SUPABASE_ANON_KEY',
    defaultValue: 'sb_publishable_GjqG64b82uXaDQzGJmo3Aw_pIcpjxKb',
  );

  // Supabase Storage Buckets
  static const String storagePodcastsBucket = 'podcasts';
  static const String storageDocumentsBucket = 'documents';

  // Maishapay Configuration publique
  static const String maishapayPublicKey = String.fromEnvironment(
    'MAISHAPAY_PUBLIC_KEY',
    defaultValue: '',
  );

  // Liens officiels
  static const String churchWebsite = 'https://eecc-eglise.org';
}
