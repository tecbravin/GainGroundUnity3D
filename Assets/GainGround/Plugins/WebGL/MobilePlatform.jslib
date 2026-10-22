mergeInto(LibraryManager.library, {
  GainGroundIsMobile: function () {
    return /Android|iPhone|iPad|iPod|Mobile/i.test(navigator.userAgent) ||
      (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1) ? 1 : 0;
  }
});
