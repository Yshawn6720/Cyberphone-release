window.readmeStage = (() => {
  const keptScreenParts = ['device-island', 'scr-status', 'scr-home-indicator'];

  function create(glow, gap) {
    const stage = document.createElement('div');
    stage.style.cssText = [
      'position:fixed', 'inset:0', 'z-index:2147483647', 'display:flex', 'align-items:center',
      'justify-content:center', `gap:${gap}px`,
      `background:radial-gradient(55% 42% at 50% 52%, ${glow}59, transparent 72%), radial-gradient(120% 80% at 50% 0%, #1a1530, #07070f 70%)`,
    ].join(';');
    return stage;
  }

  function present(stage, holder, width) {
    holder.classList.add('on');
    holder.style.cssText = `position:relative;inset:auto;width:${width}px;flex:none;opacity:1;transform:none;filter:drop-shadow(0 36px 48px rgba(0,0,0,.55));`;
    holder.querySelector('.device').style.width = '100%';
    const extras = holder.querySelectorAll(':scope > :not(.device)');
    for (let extraIndex = 0; extraIndex < extras.length; extraIndex++) {
      extras[extraIndex].style.display = 'none';
    }
    stage.appendChild(holder);
  }

  function slot(stage, name, width) {
    present(stage, document.querySelector(`.screen-slot[data-screen="${name}"]`), width);
  }

  function shell(stage, width) {
    const device = document.querySelector('.screen-slot[data-screen="chirper"] .device').cloneNode(true);
    const screen = device.querySelector('.device-screen');
    screen.className = 'device-screen scr';
    const parts = Array.from(screen.children);
    for (let partIndex = 0; partIndex < parts.length; partIndex++) {
      const kept = keptScreenParts.some(part => parts[partIndex].classList.contains(part));
      if (!kept) {
        parts[partIndex].remove();
      }
    }
    const holder = document.createElement('div');
    holder.className = 'screen-slot';
    holder.appendChild(device);
    present(stage, holder, width);
    return screen;
  }

  function panel(screen, selector) {
    const panelScreen = document.querySelector(selector);
    panelScreen.style.cssText += ';position:absolute;inset:0;width:100%;height:100%;margin:0;border-radius:0;box-shadow:none;padding-top:46px;box-sizing:border-box;';
    screen.prepend(panelScreen);
  }

  function finish(stage) {
    document.body.appendChild(stage);
    document.documentElement.style.overflow = 'hidden';
    window.scrollTo(0, 0);
  }

  return { create, slot, shell, panel, finish };
})();
