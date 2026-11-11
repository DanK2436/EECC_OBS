import React from "react";
import { Navbar } from "@/components/layout/Navbar";
import { Footer } from "@/components/layout/Footer";
import { HeroBanner } from "@/components/home/HeroBanner";
import { LiveCard } from "@/components/home/LiveCard";
import { SchedulesSection } from "@/components/home/SchedulesSection";
import { LatestSermons } from "@/components/home/LatestSermons";
import { DonationCTA } from "@/components/home/DonationCTA";

export default function HomePage() {
  return (
    <>
      <Navbar />
      <main className="flex-grow">
        <HeroBanner />
        <LiveCard isLive={false} />
        <SchedulesSection />
        <LatestSermons />
        <DonationCTA />
      </main>
      <Footer />
    </>
  );
}
