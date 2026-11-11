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
    defaultValue: 'MP-LIVEPK-DOw\$T0SnM/SekDy32h0M5\$A6Z2H350e.TJSq1HIRT70jndZdocX.v53cSIcxy65\$qD2ThvocUy\$EYn10vD\$3S80\$Mh4SnfByyHSH.Fazv1Dfmc2uy9sjE7VG',
  );

  // Liens officiels
  static const String churchWebsite = 'https://eecc-eglise.org';
}
