import React, { useEffect, useState } from 'react';
import api from '../api';

interface ThumbnailImageProps {
  assetId: string;
  alt: string;
  className?: string;
  onError?: () => void;
}

const ThumbnailImage: React.FC<ThumbnailImageProps> = ({ assetId, alt, className, onError }) => {
  const [imageSrc, setImageSrc] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);

  useEffect(() => {
    let isMounted = true;

    const loadThumbnail = async () => {
      try {
        setLoading(true);
        setError(false);
        
        const blob = await api.getAlbumThumbnailBlob(assetId);
        
        if (!isMounted) return;
        
        if (blob) {
          const objectUrl = URL.createObjectURL(blob);
          setImageSrc(objectUrl);
        } else {
          setError(true);
          onError?.();
        }
      } catch (err) {
        if (!isMounted) return;
        console.error('Error loading thumbnail:', err);
        setError(true);
        onError?.();
      } finally {
        if (isMounted) {
          setLoading(false);
        }
      }
    };

    loadThumbnail();

    return () => {
      isMounted = false;
      if (imageSrc) {
        URL.revokeObjectURL(imageSrc);
      }
    };
  }, [assetId, onError]);

  // Cleanup object URL when component unmounts or imageSrc changes
  useEffect(() => {
    return () => {
      if (imageSrc) {
        URL.revokeObjectURL(imageSrc);
      }
    };
  }, [imageSrc]);

  if (loading) {
    return (
      <div className={`${className} d-flex align-items-center justify-content-center bg-light`}>
        <div className="spinner-border spinner-border-sm" role="status">
          <span className="visually-hidden">Loading...</span>
        </div>
      </div>
    );
  }

  if (error || !imageSrc) {
    return (
      <div className={`${className} d-flex align-items-center justify-content-center bg-light`}>
        <span className="text-muted">No thumbnail</span>
      </div>
    );
  }

  return (
    <img
      src={imageSrc}
      alt={alt}
      className={className}
      onError={() => {
        setError(true);
        onError?.();
      }}
    />
  );
};

export default ThumbnailImage;