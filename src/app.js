let mockDownloadInterval = null;
window.currentDownloadMethod = 'text';

window.PopupAPI = {
  elements: {
    overlay: document.getElementById('progress-popup'),
    content: document.getElementById('popup-content'),
    title: document.getElementById('popup-title'),
    status: document.getElementById('popup-status'),
    progressBar: document.getElementById('popup-progress-bar'),
    progressTop: document.getElementById('popup-progress-top'),
    percentage: document.getElementById('popup-percentage')
  },

  show: function(title, initialStatus = "Starting...") {
    this.elements.title.textContent = title;
    this.elements.status.textContent = initialStatus;
    this.update(0); 
    
    // Remplacement du CSS custom par les classes Tailwind
    this.elements.overlay.classList.remove('opacity-0', 'pointer-events-none');
    this.elements.overlay.classList.add('opacity-100', 'pointer-events-auto');
    
    this.elements.content.classList.remove('scale-95', 'translate-y-4');
    this.elements.content.classList.add('scale-100', 'translate-y-0');
  },

  update: function(percent, statusText = null) {
    const clamped = Math.min(Math.max(percent, 0), 100);
    this.elements.progressBar.style.width = `${clamped}%`;
    this.elements.progressTop.style.width = `${clamped}%`;
    this.elements.percentage.textContent = `${Math.round(clamped)}%`;
    
    if (statusText) this.elements.status.textContent = statusText;
  },

  hide: function() {
    this.elements.overlay.classList.remove('opacity-100', 'pointer-events-auto');
    this.elements.overlay.classList.add('opacity-0', 'pointer-events-none');
    
    this.elements.content.classList.remove('scale-100', 'translate-y-0');
    this.elements.content.classList.add('scale-95', 'translate-y-4');
  }
};

const pages = {
  'upload': document.getElementById('page-upload'),
  'download': document.getElementById('page-download'),
  'settings': document.getElementById('page-settings')
};

const navButtons = {
  'upload': document.getElementById('nav-upload'),
  'download': document.getElementById('nav-download'),
  'settings': document.getElementById('nav-settings')
};

window.switchTab = function(tabId) {
  Object.entries(navButtons).forEach(([key, btn]) => {
    if (key === tabId) {
      btn.classList.add('bg-white/10', 'text-white', 'shadow-sm');
      btn.classList.remove('text-gray-400', 'hover:bg-white/5', 'hover:text-gray-200');
    } else {
      btn.classList.remove('bg-white/10', 'text-white', 'shadow-sm');
      btn.classList.add('text-gray-400', 'hover:bg-white/5', 'hover:text-gray-200');
    }
  });

  Object.values(pages).forEach(page => {
    page.classList.add('hidden');
    page.classList.remove('page-enter');
  });
  
  pages[tabId].classList.remove('hidden');
  void pages[tabId].offsetWidth; 
  pages[tabId].classList.add('page-enter');

  if (tabId === 'settings' && typeof window.loadSettings === 'function') {
    window.loadSettings();
  }
}


window.setDlMethod = function(method) {
  window.currentDownloadMethod = method;
  const dlTextArea = document.getElementById('dl-text-area');
  const dlFileArea = document.getElementById('dl-file-area');
  const tabDlText = document.getElementById('tab-dl-text');
  const tabDlFile = document.getElementById('tab-dl-file');

  if (method === 'text') {
    dlTextArea.classList.remove('hidden');
    dlFileArea.classList.add('hidden', 'flex');
    tabDlText.classList.add('bg-white/10', 'text-white');
    tabDlText.classList.remove('text-gray-400', 'hover:text-gray-200');
    tabDlFile.classList.remove('bg-white/10', 'text-white');
    tabDlFile.classList.add('text-gray-400', 'hover:text-gray-200');
  } else {
    dlTextArea.classList.add('hidden');
    dlFileArea.classList.remove('hidden', 'flex');
    tabDlFile.classList.add('bg-white/10', 'text-white');
    tabDlFile.classList.remove('text-gray-400', 'hover:text-gray-200');
    tabDlText.classList.remove('bg-white/10', 'text-white');
    tabDlText.classList.add('text-gray-400', 'hover:text-gray-200');
  }
}

/*document.getElementById('btn-cancel-task').addEventListener('click', () => {
  if (mockDownloadInterval) {
    clearInterval(mockDownloadInterval);
  }
  window.PopupAPI.hide();

});
TODO here
*/