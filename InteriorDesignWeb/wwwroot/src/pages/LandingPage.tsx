import { Header } from '@/components/Header';
import { LoginModal } from '@/components/LoginModal';
import { HeroSection } from './landing/HeroSection';
import { FeaturesSection } from './landing/FeaturesSection';
import { GallerySection } from './landing/GallerySection';
import { FooterSection } from './landing/FooterSection';

export default function LandingPage() {
  return (
    <div className="relative">
      <Header />
      <LoginModal />

      {/* Snap scroll container */}
      <div className="snap-container" style={{ height: '100vh', overflowY: 'scroll' }}>
        <HeroSection />
        <FeaturesSection />
        <GallerySection />
        <FooterSection />
      </div>
    </div>
  );
}
