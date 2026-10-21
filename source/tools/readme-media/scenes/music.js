window.readmeMusic = (screen, artwork) => {
  const track = { title: 'Petals over Kugane', artist: 'Lyra Fenn', startSeconds: 30, lengthSeconds: 218 };
  const lyrics = [
    'Lanterns on the water',
    'we stayed past the last bell',
    'the tide pulled the city in',
    'your linkpearl humming low',
    'meet me on the red bridge',
    'petals in the harbor',
    'don\'t let the night run out',
    'we\'ll find it in the morning',
    'one more song before the dawn',
    'and the boats come home',
  ];
  const loopSeconds = 13;
  const lyricsInSeconds = 4.6;
  const lyricsOutSeconds = 12.3;
  const fadeSeconds = 0.45;
  const lineSeconds = 1.25;
  const firstActiveLine = 2;

  const icons = {
    heart: '<svg viewBox="0 0 24 24" fill="currentColor"><path d="M12 21s-7.5-4.6-9.6-9.2C.9 8.4 3 4.5 6.8 4.5c2.1 0 3.6 1.1 5.2 3 1.6-1.9 3.1-3 5.2-3 3.8 0 5.9 3.9 4.4 7.3C19.5 16.4 12 21 12 21z"/></svg>',
    more: '<svg viewBox="0 0 24 24" fill="currentColor"><circle cx="5" cy="12" r="2"/><circle cx="12" cy="12" r="2"/><circle cx="19" cy="12" r="2"/></svg>',
    previous: '<svg viewBox="0 0 24 24" fill="currentColor"><rect x="3" y="4" width="3" height="16" rx="1"/><path d="M21 4.5v15L8 12z"/></svg>',
    pause: '<svg viewBox="0 0 24 24" fill="currentColor"><rect x="5" y="3" width="4.5" height="18" rx="2.2"/><rect x="14.5" y="3" width="4.5" height="18" rx="2.2"/></svg>',
    next: '<svg viewBox="0 0 24 24" fill="currentColor"><rect x="18" y="4" width="3" height="16" rx="1"/><path d="M3 4.5v15L16 12z"/></svg>',
    volumeLow: '<svg viewBox="0 0 24 24" fill="currentColor"><path d="M4 9h4l5-4v14l-5-4H4z"/></svg>',
    volumeHigh: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><path d="M4 9h4l5-4v14l-5-4H4z" fill="currentColor" stroke="none"/><path d="M16 9a4 4 0 0 1 0 6M18.5 6.5a7.5 7.5 0 0 1 0 11"/></svg>',
    quote: '<svg viewBox="0 0 24 24" fill="currentColor"><path d="M4 11.5C4 8 6 5.6 9.4 5l.6 1.6C8 7.3 7.2 8.6 7.1 10H10v7H4zM14 11.5c0-3.5 2-5.9 5.4-6.5l.6 1.6c-2 .7-2.8 2-2.9 3.4H20v7h-6z"/></svg>',
    headphones: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round"><path d="M4 16v-3a8 8 0 0 1 16 0v3"/><rect x="3" y="14" width="4" height="7" rx="1.5" fill="currentColor"/><rect x="17" y="14" width="4" height="7" rx="1.5" fill="currentColor"/></svg>',
    list: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round"><path d="M9 6h12M9 12h12M9 18h12"/><circle cx="4" cy="6" r="1.2" fill="currentColor"/><circle cx="4" cy="12" r="1.2" fill="currentColor"/><circle cx="4" cy="18" r="1.2" fill="currentColor"/></svg>',
  };

  const style = document.createElement('style');
  style.textContent = `
    .rm-music { position:absolute; inset:0; background:#140c10; color:#fff; font-family:inherit; }
    .rm-sheet { position:absolute; left:0; right:0; top:48px; bottom:0; border-radius:26px 26px 0 0; display:flex; flex-direction:column; padding:10px 20px 26px; background:linear-gradient(180deg,#6b3a4b 0%,#4a2733 55%,#2c1820 100%); box-shadow:0 -1px 0 rgba(255,255,255,.08); }
    .rm-grabber { width:38px; height:5px; border-radius:9px; background:rgba(255,255,255,.35); margin:0 auto 14px; flex:none; }
    .rm-top { position:relative; flex:1; min-height:0; }
    .rm-layer { position:absolute; inset:0; display:flex; flex-direction:column; }
    .rm-art { width:100%; aspect-ratio:1; border-radius:14px; background-size:cover; background-position:center 30%; box-shadow:0 18px 40px -12px rgba(0,0,0,.6); margin-top:18px; }
    .rm-meta { display:flex; align-items:center; gap:10px; margin-top:auto; }
    .rm-meta div { flex:1; min-width:0; }
    .rm-meta b { display:block; font-size:16px; font-weight:700; white-space:nowrap; overflow:hidden; text-overflow:ellipsis; }
    .rm-meta span { display:block; font-size:14px; color:rgba(255,255,255,.6); margin-top:2px; }
    .rm-round { width:34px; height:34px; border-radius:50%; background:rgba(255,255,255,.14); display:grid; place-items:center; flex:none; }
    .rm-round svg { width:15px; height:15px; }
    .rm-head { display:flex; align-items:center; gap:10px; flex:none; }
    .rm-head .rm-thumb { width:52px; height:52px; border-radius:8px; background-size:cover; background-position:center 30%; flex:none; }
    .rm-lyrics { position:relative; flex:1; overflow:hidden; margin-top:12px; -webkit-mask-image:linear-gradient(180deg,transparent,#000 14%,#000 80%,transparent); mask-image:linear-gradient(180deg,transparent,#000 14%,#000 80%,transparent); }
    .rm-lines { position:absolute; left:0; right:0; top:0; transition:transform .55s cubic-bezier(.2,.7,.2,1); }
    .rm-lines p { margin:0 0 16px; font-size:20px; line-height:1.2; font-weight:800; letter-spacing:-.01em; color:rgba(255,255,255,.32); transition:color .4s; }
    .rm-lines p.on { color:#fff; }
    .rm-progress { margin-top:18px; flex:none; }
    .rm-bar { height:5px; border-radius:9px; background:rgba(255,255,255,.22); overflow:hidden; }
    .rm-bar i { display:block; height:100%; background:rgba(255,255,255,.9); border-radius:9px; }
    .rm-times { display:flex; justify-content:space-between; font-size:11px; color:rgba(255,255,255,.55); margin-top:7px; font-variant-numeric:tabular-nums; }
    .rm-controls { display:flex; justify-content:center; align-items:center; gap:46px; margin:20px 0 22px; flex:none; }
    .rm-controls svg { width:30px; height:30px; }
    .rm-controls .rm-pause svg { width:38px; height:38px; }
    .rm-volume { display:flex; align-items:center; gap:10px; flex:none; color:rgba(255,255,255,.6); }
    .rm-volume svg { width:14px; height:14px; flex:none; }
    .rm-volume .rm-bar { flex:1; }
    .rm-volume .rm-bar i { width:46%; }
    .rm-tools { display:flex; justify-content:space-between; align-items:center; margin-top:20px; padding:0 8px; flex:none; color:rgba(255,255,255,.7); }
    .rm-tool { width:42px; height:42px; border-radius:50%; display:grid; place-items:center; transition:background .35s, color .35s; }
    .rm-tool svg { width:18px; height:18px; }
    .rm-tool.on { background:#f2eef0; color:#3a2029; }
    .rm-island-art { width:22px; height:22px; border-radius:50%; background-size:cover; background-position:center 30%; }
    .rm-eq { display:flex; gap:2px; align-items:center; height:14px; }
    .rm-eq i { width:3px; height:100%; border-radius:2px; background:#3ddc72; transform-origin:center; animation:rm-eq .8s ease-in-out infinite alternate; }
    .rm-eq i:nth-child(2) { animation-delay:-.3s; } .rm-eq i:nth-child(3) { animation-delay:-.55s; } .rm-eq i:nth-child(4) { animation-delay:-.15s; }
    @keyframes rm-eq { from { transform:scaleY(.3); } to { transform:scaleY(1); } }
  `;
  document.head.appendChild(style);

  const formatTime = totalSeconds => `${Math.floor(totalSeconds / 60)}:${String(Math.floor(totalSeconds % 60)).padStart(2, '0')}`;
  const artStyle = `background-image:url('${artwork}')`;

  const root = document.createElement('div');
  root.className = 'rm-music';
  root.innerHTML = `
    <div class="rm-sheet">
      <div class="rm-grabber"></div>
      <div class="rm-top">
        <div class="rm-layer rm-now">
          <div class="rm-art" style="${artStyle}"></div>
          <div class="rm-meta"><div><b>${track.title}</b><span>${track.artist}</span></div><span class="rm-round">${icons.heart}</span><span class="rm-round">${icons.more}</span></div>
        </div>
        <div class="rm-layer rm-words" style="opacity:0">
          <div class="rm-head rm-meta"><span class="rm-thumb" style="${artStyle}"></span><div><b>${track.title}</b><span>${track.artist}</span></div><span class="rm-round">${icons.heart}</span><span class="rm-round">${icons.more}</span></div>
          <div class="rm-lyrics"><div class="rm-lines">${lyrics.map(line => `<p>${line}</p>`).join('')}</div></div>
        </div>
      </div>
      <div class="rm-progress"><div class="rm-bar"><i></i></div><div class="rm-times"><span class="rm-elapsed"></span><span class="rm-remaining"></span></div></div>
      <div class="rm-controls"><span>${icons.previous}</span><span class="rm-pause">${icons.pause}</span><span>${icons.next}</span></div>
      <div class="rm-volume">${icons.volumeLow}<div class="rm-bar"><i></i></div>${icons.volumeHigh}</div>
      <div class="rm-tools"><span class="rm-tool rm-quote">${icons.quote}</span><span class="rm-tool">${icons.headphones}</span><span class="rm-tool">${icons.list}</span></div>
    </div>`;
  screen.prepend(root);

  const island = screen.querySelector('.device-island');
  island.style.cssText += ';display:flex;align-items:center;justify-content:space-between;padding:0 7px;box-sizing:border-box;width:44%;';
  island.innerHTML = `<span class="rm-island-art" style="${artStyle}"></span><span class="rm-eq"><i></i><i></i><i></i><i></i></span>`;

  const nowLayer = root.querySelector('.rm-now');
  const wordsLayer = root.querySelector('.rm-words');
  const quoteTool = root.querySelector('.rm-quote');
  const progressFill = root.querySelector('.rm-progress .rm-bar i');
  const elapsedText = root.querySelector('.rm-elapsed');
  const remainingText = root.querySelector('.rm-remaining');
  const lineContainer = root.querySelector('.rm-lines');
  const lines = root.querySelectorAll('.rm-lines p');
  const lyricsBox = root.querySelector('.rm-lyrics');
  let activeLine = -1;

  const clamp = value => Math.min(1, Math.max(0, value));
  const start = performance.now();

  function frame(now) {
    const time = ((now - start) / 1000) % loopSeconds;
    const elapsed = track.startSeconds + time;
    progressFill.style.width = `${(elapsed / track.lengthSeconds) * 100}%`;
    elapsedText.textContent = formatTime(elapsed);
    remainingText.textContent = `-${formatTime(track.lengthSeconds - elapsed)}`;

    const fadeIn = clamp((time - lyricsInSeconds) / fadeSeconds);
    const fadeOut = clamp((time - lyricsOutSeconds) / fadeSeconds);
    const mix = fadeIn - fadeOut;
    wordsLayer.style.opacity = String(mix);
    nowLayer.style.opacity = String(1 - mix);
    quoteTool.classList.toggle('on', mix > 0.5);

    const line = time < lyricsInSeconds ? firstActiveLine : firstActiveLine + Math.floor((time - lyricsInSeconds) / lineSeconds);
    const boundedLine = Math.min(line, lines.length - 1);
    if (boundedLine !== activeLine) {
      if (activeLine >= 0) {
        lines[activeLine].classList.remove('on');
      }
      lines[boundedLine].classList.add('on');
      const offset = lines[boundedLine].offsetTop - lyricsBox.clientHeight * 0.32;
      lineContainer.style.transform = `translateY(${-Math.max(0, offset)}px)`;
      activeLine = boundedLine;
    }
    requestAnimationFrame(frame);
  }
  requestAnimationFrame(frame);
};
