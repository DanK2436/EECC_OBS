import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'src/constants/colors.dart';
import 'src/views/home_screen.dart';

void main() async {
  WidgetsFlutterBinding.ensureInitialized();

  // Mode immersif sombre pour l'application caméra
  SystemChrome.setSystemUIOverlayStyle(
    const SystemUiOverlayStyle(
      statusBarColor: Colors.transparent,
      statusBarIconBrightness: Brightness.light,
      systemNavigationBarColor: EeccCamColors.background,
      systemNavigationBarIconBrightness: Brightness.light,
    ),
  );

  runApp(const EeccCameraApp());
}

class EeccCameraApp extends StatelessWidget {
  const EeccCameraApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'EECC Caméra',
      debugShowCheckedModeBanner: false,
      theme: ThemeData(
        useMaterial3: true,
        scaffoldBackgroundColor: EeccCamColors.background,
        colorScheme: ColorScheme.fromSeed(
          seedColor: EeccCamColors.primary,
          brightness: Brightness.dark,
        ),
      ),
      home: const CamHomeScreen(),
    );
  }
}
