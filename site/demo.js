// Demo playlist: swaps the poster for a youtube-nocookie player only when someone presses play,
// so the page loads nothing from YouTube until then. Without JavaScript the playlist links still work.
(() => {
  const grid = document.querySelector('.demo-grid');
  if (!grid) return;

  const lang = grid.dataset.lang || 'en';
  const text = {
    en: { play: 'Play', soon: 'This video is being published. Check back soon.' },
    es: { play: 'Ver', soon: 'Este video se está publicando. Vuelva pronto.' },
  }[lang];

  const screen = document.getElementById('demo-screen');
  const poster = document.getElementById('demo-poster');
  const play = document.getElementById('demo-play');
  const name = document.getElementById('demo-name');
  const desc = document.getElementById('demo-text');
  const links = [...grid.querySelectorAll('.playlist a')];
  let current = links.find((a) => a.getAttribute('aria-current') === 'true') || links[0];

  const status = document.createElement('p');
  status.className = 'demo-status';
  status.setAttribute('aria-live', 'polite');
  desc.after(status);

  function showPoster() {
    screen.querySelector('iframe')?.remove();
    poster.hidden = false;
    play.hidden = false;
  }

  function select(link) {
    current.removeAttribute('aria-current');
    link.setAttribute('aria-current', 'true');
    current = link;
    poster.src = link.dataset.poster;
    poster.alt = link.dataset.alt;
    name.textContent = link.querySelector('.pl-name').textContent;
    desc.textContent = link.querySelector('.pl-text').textContent;
    play.querySelector('.play-stamp').innerHTML = `${text.play}<small>${link.dataset.time}</small>`;
    status.textContent = '';
    showPoster();
  }

  function start() {
    const id = current.dataset.yt;
    if (!id) {
      status.textContent = text.soon;
      return;
    }
    const frame = document.createElement('iframe');
    frame.src = `https://www.youtube-nocookie.com/embed/${encodeURIComponent(id)}?autoplay=1&rel=0&hl=${lang}`;
    frame.title = name.textContent;
    frame.allow = 'autoplay; encrypted-media; picture-in-picture; fullscreen';
    frame.allowFullscreen = true;
    poster.hidden = true;
    play.hidden = true;
    screen.append(frame);
    frame.focus();
  }

  play.addEventListener('click', start);
  for (const link of links) {
    link.addEventListener('click', (event) => {
      event.preventDefault();
      select(link);
      start();
      // On narrow screens the playlist sits below the player; bring the player back into view.
      const reduce = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
      screen.scrollIntoView({ block: 'nearest', behavior: reduce ? 'auto' : 'smooth' });
    });
  }
})();
