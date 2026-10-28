mergeInto(LibraryManager.library, {
  GainGroundIsMobile: function () {
    return /Android|iPhone|iPad|iPod|Mobile/i.test(navigator.userAgent) ||
      (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1) ||
      (navigator.maxTouchPoints > 0 && window.matchMedia &&
        window.matchMedia('(pointer: coarse)').matches) ? 1 : 0;
  },
  GainGroundLockLandscape: function () {
    if (window.gainGroundTryLandscape) {
      window.gainGroundTryLandscape(false);
      return;
    }
    var fullscreenAttempted = false;
    var lockPending = false;
    var lock = function (fromGesture) {
      if (!screen.orientation || !screen.orientation.lock || lockPending) return;
      var canvas = Module.canvas;
      var fullscreen = document.fullscreenElement || document.webkitFullscreenElement;
      var portrait = window.innerHeight > window.innerWidth;
      if (portrait && !fullscreen && !fromGesture) return;
      if (portrait && !fullscreen && fullscreenAttempted) return;
      var request = canvas && (canvas.requestFullscreen || canvas.webkitRequestFullscreen);
      var enterFullscreen = portrait && fromGesture && !fullscreen && request;
      if (portrait && fromGesture) {
        fullscreenAttempted = true;
        document.removeEventListener('pointerdown', window.gainGroundLandscapeHandler, false);
      }
      lockPending = true;
      try {
        var entered = enterFullscreen ? request.call(canvas) : null;
        Promise.resolve(entered).then(function () {
          return screen.orientation.lock('landscape');
        }).catch(function () {
          // Some mobile browsers do not support orientation locking.
        }).then(function () {
          lockPending = false;
        });
      } catch (error) {
        lockPending = false;
      }
    };
    window.gainGroundTryLandscape = lock;
    window.gainGroundLandscapeHandler = function () {
      if (window.innerHeight > window.innerWidth) lock(true);
    };
    document.addEventListener('pointerdown', window.gainGroundLandscapeHandler, false);
    lock(false);
  },
  GainGroundStorageGet: function (key) {
    try {
      var value = window.localStorage.getItem(UTF8ToString(key));
      if (value === null) return 0;
      var size = lengthBytesUTF8(value) + 1;
      var buffer = _malloc(size);
      stringToUTF8(value, buffer, size);
      return buffer;
    } catch (error) {
      console.warn('Gain Ground: browser storage unavailable', error);
      return 0;
    }
  },
  GainGroundStorageSet: function (key, value) {
    try {
      var name = UTF8ToString(key);
      var content = UTF8ToString(value);
      window.localStorage.setItem(name, content);
      return window.localStorage.getItem(name) === content ? 1 : 0;
    } catch (error) {
      console.warn('Gain Ground: could not save to browser storage', error);
      return 0;
    }
  },
  GainGroundStorageDelete: function (key) {
    try {
      window.localStorage.removeItem(UTF8ToString(key));
      return 1;
    } catch (error) {
      console.warn('Gain Ground: could not remove browser save', error);
      return 0;
    }
  }
});
