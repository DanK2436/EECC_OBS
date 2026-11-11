import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'src/constants/colors.dart';
import 'src/constants/config.dart';
import 'src/views/main_navigation_shell.dart';

void main() async {
  WidgetsFlutterBinding.ensureInitialized();

  // Barre de statut système transparente et moderne
  SystemChrome.setSystemUIOverlayStyle(
    const SystemUiOverlayStyle(
      statusBarColor: Colors.transparent,
      statusBarIconBrightness: Brightness.dark,
      systemNavigationBarColor: EeccColors.surface,
      systemNavigationBarIconBrightness: Brightness.dark,
    ),
  );

  runApp(const EeccTvApp());
}

class EeccTvApp extends StatelessWidget {
  const EeccTvApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: EeccConfig.appName,
      debugShowCheckedModeBanner: false,
      theme: ThemeData(
        useMaterial3: true,
        scaffoldBackgroundColor: EeccColors.background,
        colorScheme: ColorScheme.fromSeed(
          seedColor: EeccColors.primary,
          primary: EeccColors.primary,
          surface: EeccColors.surface,
          background: EeccColors.background,
        ),
        appBarTheme: const AppBarTheme(
          backgroundColor: EeccColors.surface,
          foregroundColor: EeccColors.textMain,
          centerTitle: false,
          elevation: 0,
        ),
      ),
      home: const MainNavigationShell(),
    );
  }
}
