import React, { useState, useEffect, useRef } from 'react';
import './App.css';

const API_BASE_URL = 'http://localhost:5014/api';
const HUB_URL = 'http://localhost:5014/crawlerHub';

function App() {
  const [messages, setMessages] = useState([]);
  const [inputMessage, setInputMessage] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [connection, setConnection] = useState(null);
  const [crawlStatus, setCrawlStatus] = useState(null);
  const [conversationId] = useState(() => `conv_${Date.now()}`);
  const messagesEndRef = useRef(null);

  // Documentation sources management
  const [showDocPanel, setShowDocPanel] = useState(false);
  const [docSources, setDocSources] = useState(() => {
    const saved = localStorage.getItem('docSources');
    return saved ? JSON.parse(saved) : [];
  });
  const [newDocName, setNewDocName] = useState('');
  const [newDocUrl, setNewDocUrl] = useState('');

  // Initialize SignalR connection
  useEffect(() => {
    
  }, []);

  

  // Save doc sources to localStorage whenever they change
  useEffect(() => {
    localStorage.setItem('docSources', JSON.stringify(docSources));
  }, [docSources]);

  // Auto-scroll to bottom
  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages]);

  const addDocumentationSource = (e) => {
    e.preventDefault();
    if (!newDocName.trim() || !newDocUrl.trim()) return;

    const newSource = {
      id: Date.now(),
      name: newDocName.trim(),
      url: newDocUrl.trim(),
      status: 'pending',
      addedAt: new Date().toISOString()
    };

    setDocSources(prev => [...prev, newSource]);
    setNewDocName('');
    setNewDocUrl('');
  };

  const crawlDocumentation = async (source) => {
    setDocSources(prev => prev.map(s =>
      s.id === source.id ? { ...s, status: 'crawling' } : s
    ));

    try {
      const response = await fetch(`${API_BASE_URL}/chat/crawl`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          indexName: source.name,
          url: source.url,
          connectionId: '1'
        })
      });

      const data = await response.json();

      if (data.success) {
        setDocSources(prev => prev.map(s =>
          s.id === source.id ? { ...s, status: 'completed', crawledAt: new Date().toISOString() } : s
        ));
      } else {
        setDocSources(prev => prev.map(s =>
          s.id === source.id ? { ...s, status: 'error' } : s
        ));
      }
    } catch (error) {
      console.error('Error crawling:', error);
      setDocSources(prev => prev.map(s =>
        s.id === source.id ? { ...s, status: 'error' } : s
      ));
    }
  };

  const removeDocumentationSource = (id) => {
    setDocSources(prev => prev.filter(s => s.id !== id));
  };

  const sendMessage = async (e) => {
    e.preventDefault();
    
    if (!inputMessage.trim() || isLoading) return;

    const userMessage = {
      role: 'user',
      content: inputMessage,
      timestamp: new Date()
    };

    setMessages(prev => [...prev, userMessage]);
    setInputMessage('');
    setIsLoading(true);

    try {
      const response = await fetch(`${API_BASE_URL}/chat/message`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          message: inputMessage,
          conversationId: conversationId,
          connectionId:  '1'
        })
      });

      const data = await response.json();

      if (data.success) {
        const assistantMessage = {
          role: 'assistant',
          content: data.message,
          sources: data.sources || [],
          timestamp: new Date()
        };
        setMessages(prev => [...prev, assistantMessage]);
      } else {
        throw new Error(data.error || 'Failed to get response');
      }
    } catch (error) {
      console.error('Error sending message:', error);
      const errorMessage = {
        role: 'assistant',
        content: `Error: ${error.message}`,
        timestamp: new Date(),
        isError: true
      };
      setMessages(prev => [...prev, errorMessage]);
    } finally {
      setIsLoading(false);
    }
  };

  const isCrawlCommand = (text) => {
    return text.trim().toUpperCase().startsWith('CRAWL:');
  };

  return (
    <div className="app-container">
      <header className="app-header">
        <h1>Technical Documentation Assistant</h1>
        <p className="subtitle">Manage your documentation sources and ask questions</p>
        <button
          className="toggle-panel-btn"
          onClick={() => setShowDocPanel(!showDocPanel)}
        >
          {showDocPanel ? 'Hide' : 'Show'} Documentation Panel
        </button>
      </header>

      {showDocPanel && (
        <div className="doc-panel">
          <div className="doc-panel-header">
            <h2>Documentation Sources</h2>
            <p>Add and manage documentation to crawl</p>
          </div>

          <form onSubmit={addDocumentationSource} className="add-doc-form">
            <div className="form-group">
              <label htmlFor="docName">Name</label>
              <input
                id="docName"
                type="text"
                value={newDocName}
                onChange={(e) => setNewDocName(e.target.value)}
                placeholder="e.g., React Documentation"
                className="doc-input"
              />
            </div>
            <div className="form-group">
              <label htmlFor="docUrl">URL</label>
              <input
                id="docUrl"
                type="url"
                value={newDocUrl}
                onChange={(e) => setNewDocUrl(e.target.value)}
                placeholder="https://docs.example.com"
                className="doc-input"
              />
            </div>
            <button type="submit" className="add-doc-btn">
              Add Documentation
            </button>
          </form>

          <div className="doc-sources-list">
            {docSources.length === 0 ? (
              <p className="no-sources">No documentation sources added yet.</p>
            ) : (
              docSources.map(source => (
                <div key={source.id} className={`doc-source-item status-${source.status}`}>
                  <div className="doc-source-info">
                    <h3>{source.name}</h3>
                    <a href={source.url} target="_blank" rel="noopener noreferrer">
                      {source.url}
                    </a>
                    <span className={`status-badge ${source.status}`}>
                      {source.status}
                    </span>
                  </div>
                  <div className="doc-source-actions">
                    <button
                      onClick={() => crawlDocumentation(source)}
                      disabled={source.status === 'crawling'}
                      className="crawl-btn"
                    >
                      {source.status === 'crawling' ? 'Crawling...' : 'Crawl'}
                    </button>
                    <button
                      onClick={() => removeDocumentationSource(source.id)}
                      className="remove-btn"
                    >
                      Remove
                    </button>
                  </div>
                </div>
              ))
            )}
          </div>
        </div>
      )}

      {crawlStatus && (
        <div className={`crawl-status status-${crawlStatus.status}`}>
          <div className="status-content">
            <span className="status-icon">
              {crawlStatus.status === 'completed' ? '✓' : '⟳'}
            </span>
            <div className="status-text">
              <strong>{crawlStatus.status.toUpperCase()}</strong>
              <p>{crawlStatus.message}</p>
            </div>
            {crawlStatus.progress !== undefined && (
              <div className="progress-bar">
                <div 
                  className="progress-fill" 
                  style={{ width: `${crawlStatus.progress}%` }}
                />
              </div>
            )}
          </div>
        </div>
      )}

      <div className="chat-container">
        <div className="messages-list">
          {messages.length === 0 && (
            <div className="welcome-message">
              <h2>Welcome!</h2>
              <p>Start by crawling technical documentation:</p>
              <code>CRAWL: https://docs.example.com</code>
              <p>Then ask questions about the documentation!</p>
            </div>
          )}

          {messages.map((msg, index) => (
            <div 
              key={index} 
              className={`message ${msg.role} ${msg.isSystem ? 'system' : ''} ${msg.isError ? 'error' : ''}`}
            >
              <div className="message-avatar">
                {msg.role === 'user' ? '' : ''}
              </div>
              <div className="message-content">
                <div className="message-text">{msg.content}</div>
                
                {msg.sources && msg.sources.length > 0 && (
                  <div className="sources">
                    <div className="sources-header">Sources:</div>
                    {msg.sources.map((source, idx) => (
                      <div key={idx} className="source-item">
                        <a href={source.url} target="_blank" rel="noopener noreferrer">
                          {source.title}
                        </a>
                        {source.snippet && (
                          <p className="source-snippet">{source.snippet}...</p>
                        )}
                      </div>
                    ))}
                  </div>
                )}

                <div className="message-timestamp">
                  {msg.timestamp.toLocaleTimeString()}
                </div>
              </div>
            </div>
          ))}

          {isLoading && (
            <div className="message assistant typing">
              <div className="message-avatar"></div>
              <div className="message-content">
                <div className="typing-indicator">
                  <span></span>
                  <span></span>
                  <span></span>
                </div>
              </div>
            </div>
          )}

          <div ref={messagesEndRef} />
        </div>

        <form onSubmit={sendMessage} className="input-form">
          <input
            type="text"
            value={inputMessage}
            onChange={(e) => setInputMessage(e.target.value)}
            placeholder={isCrawlCommand(inputMessage) 
              ? "Type full URL after CRAWL: command..." 
              : "Ask a question or type CRAWL: URL"}
            disabled={isLoading}
            className="message-input"
          />
          <button 
            type="submit" 
            disabled={isLoading || !inputMessage.trim()}
            className="send-button"
          >
            {isCrawlCommand(inputMessage) ? 'Crawl' : 'Send'}
          </button>
        </form>
      </div>
    </div>
  );
}

export default App;