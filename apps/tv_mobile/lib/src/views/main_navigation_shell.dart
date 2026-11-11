import 'package:flutter/material.dart';
import '../constants/colors.dart';
import 'home/home_screen.dart';
import 'live/live_screen.dart';
import 'media/media_library_screen.dart';
import 'bible/bible_screen.dart';
import 'profile/profile_screen.dart';

class MainNavigationShell extends StatefulWidget {
  const MainNavigationShell({super.key});

  @override
  State<MainNavigationShell> createState() => _MainNavigationShellState();
}

class _MainNavigationShellState extends State<MainNavigationShell> {
  int _currentIndex = 0;

  void _onTabTapped(int index) {
    setState(() => _currentIndex = index);
  }

  @override
  Widget build(BuildContext context) {
    final List<Widget> screens = [
      HomeScreen(onNavigateTab: _onTabTapped),
      const LiveScreen(),
      const MediaLibraryScreen(),
      const BibleScreen(),
      const ProfileScreen(),
    ];

    return Scaffold(
      body: IndexedStack(
        index: _currentIndex,
        children: screens,
      ),
      bottomNavigationBar: Container(
        decoration: const BoxDecoration(
          color: EeccColors.surface,
          border: Border(top: BorderSide(color: EeccColors.border, width: 1)),
        ),
        child: BottomNavigationBar(
          currentIndex: _currentIndex,
          onTap: _onTabTapped,
          type: BottomNavigationBarType.fixed,
          backgroundColor: EeccColors.surface,
          selectedItemColor: EeccColors.primary,
          unselectedItemColor: EeccColors.slate,
          selectedLabelStyle: const TextStyle(fontWeight: FontWeight.bold, fontSize: 11),
          unselectedLabelStyle: const TextStyle(fontSize: 11),
          items: const [
            BottomNavigationBarItem(
              icon: Icon(Icons.home_outlined),
              activeIcon: Icon(Icons.home_rounded),
              label: 'Accueil',
            ),
            BottomNavigationBarItem(
              icon: Icon(Icons.live_tv_outlined),
              activeIcon: Icon(Icons.live_tv_rounded),
              label: 'Direct',
            ),
            BottomNavigationBarItem(
              icon: Icon(Icons.podcasts_outlined),
              activeIcon: Icon(Icons.podcasts_rounded),
              label: 'Médiathèque',
            ),
            BottomNavigationBarItem(
              icon: Icon(Icons.menu_book_outlined),
              activeIcon: Icon(Icons.menu_book_rounded),
              label: 'Bible',
            ),
            BottomNavigationBarItem(
              icon: Icon(Icons.person_outline_rounded),
              activeIcon: Icon(Icons.person_rounded),
              label: 'Profil',
            ),
          ],
        ),
      ),
    );
  }
}
