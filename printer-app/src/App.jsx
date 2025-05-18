import { useState } from 'react'
import reactLogo from './assets/react.svg'
import viteLogo from '/vite.svg'
import './App.css'
import html2pdf from 'html2pdf.js'

function App() {
  const [count, setCount] = useState(0)
  const [printers, setPrinters] = useState([])
  const [selectedPrinter, setSelectedPrinter] = useState('')
  const [printer1Name, setPrinter1Name] = useState('Printer1')
  const [printer2Name, setPrinter2Name] = useState('Printer2')
  const [pdfBase64, setPdfBase64] = useState(null);

  const directPrint = (printerName) => {
    const contentDiv = document.getElementById('content');
    const html = contentDiv.innerText;

    qz.websocket.connect().then(() => {
      return qz.printers.find(printerName);
    }).then((printer) => {
      const config = qz.configs.create(printer);
      const data = [{ type: 'raw', format: 'plain', data: html }];
      return qz.print(config, data);
    }).then(() => {
      console.log('Print successful');
      qz.websocket.disconnect();
    }).catch(err => {
      console.error('Print error:', err);
      qz.websocket.disconnect();
    });
  }

  const showPrinters = async () => {
    try {
      // qz.security.setSignaturePromise(toSign => {
      //   return Promise.resolve(); // بدون امضا
      // });
      await qz.websocket.connect();
      const availablePrinters = await qz.printers.find();
      console.log('Available Printers:', availablePrinters);
      setPrinters(availablePrinters);
      await qz.websocket.disconnect();
    } catch (err) {
      console.error('Error fetching printers:', err);
      qz.websocket.disconnect();
    }
  }

  const convertAndPreviewPDF = async () => {
    const contentDiv = document.getElementById('content');

    const opt = {
      margin: 1,
      filename: 'document.pdf',
      image: { type: 'jpeg', quality: 0.98 },
      html2canvas: { scale: 2 },
      jsPDF: { unit: 'in', format: 'letter', orientation: 'portrait' }
    };

    try {
      const pdf = await html2pdf().set(opt).from(contentDiv).toPdf().get('pdf');
      const blob = await pdf.output('blob');

      const blobUrl = URL.createObjectURL(blob);
      window.open(blobUrl, '_blank');

      const reader = new FileReader();
      reader.readAsDataURL(blob);
      reader.onloadend = () => {
        const base64data = reader.result.split(',')[1];
        setPdfBase64(base64data);
      };
    } catch (err) {
      console.error('PDF generation error:', err);
    }
  };

  const printStoredPDF = async (printerName) => {
    let printerNameMultiple = printerName || selectedPrinter;
    if (!pdfBase64 || !printerNameMultiple) return;

    try {
      await qz.websocket.connect();
      const printer = await qz.printers.find(printerNameMultiple);
      const config = qz.configs.create(printer);
      const data = [{ type: 'pdf', format: 'base64', data: pdfBase64 }];
      await qz.print(config, data);
      console.log('User-confirmed print successful');
      qz.websocket.disconnect();
    } catch (err) {
      console.error('Print error:', err);
      qz.websocket.disconnect();
    }
  };

  return (
    <>
    <div id="content">
      <h1>Hello World</h1>
      <h2>This is a test</h2>
      <p>
        This is a paragraph
        that spans across
        three lines
      </p>
      <p>This is another paragraph</p>
    </div>
    <div id="footer">
    {/* <div style={{ display: 'flex', gap: '10px',marginBottom: '10px' }}>
        <button onClick={() => directPrint(printer1Name)}>
          Print via Printer1
        </button>
      <button onClick={() => directPrint(printer2Name)}>
        Print via Printer2
        </button>
      </div> */}
      <button onClick={() => {
        const contentDiv = document.getElementById('content');
        const printWindow = window.open('', '', 'width=800,height=600');
        printWindow.document.write(contentDiv.innerHTML);
        printWindow.document.close();
        printWindow.print();
      }}>
        Print Content
      </button>
      <button onClick={convertAndPreviewPDF} disabled={!selectedPrinter}>
        Preview PDF
      </button>
      {pdfBase64 && (
        <button onClick={printStoredPDF} disabled={!selectedPrinter}>
          Print Now
        </button>
      )}
      <select 
        value={selectedPrinter} 
        onChange={(e) => setSelectedPrinter(e.target.value)}
        style={{ margin: '0 10px' }}
      >
        <option value="">Select a printer</option>
        {printers.map((printer, index) => (
          <option key={index} value={printer}>
            {printer}
          </option>
        ))}
      </select>
      <button onClick={() => directPrint(selectedPrinter)} disabled={!selectedPrinter}>
        Print via Selected Printer
      </button>
      <button onClick={() => showPrinters()}>
        Refresh Printers
      </button>

      <div>
      <div style={{ display: 'flex', gap: '10px', marginBottom: '10px' }}>
        <input
          type="text"
          placeholder="Enter printer 1 name"
          value={printer1Name}
          onChange={(e) => setPrinter1Name(e.target.value)}
          style={{ padding: '0.6em 1.2em' }}
        />
        <input
          type="text" 
          placeholder="Enter printer 2 name"
          value={printer2Name}
          onChange={(e) => setPrinter2Name(e.target.value)}
          style={{ padding: '0.6em 1.2em' }}
        />
        <button onClick={() => {
          directPrint(printer1Name);
          setTimeout(() => {
            directPrint(printer2Name);
          }, 5000);
        }}>
          Print With Printer Names
        </button>
        {pdfBase64 && (
        <button onClick={() => {
          printStoredPDF(printer1Name);
          setTimeout(() => {
            printStoredPDF(printer2Name);
          }, 5000);
        }} disabled={!selectedPrinter}>
          Print Now Multiple
        </button>
      )}
      </div>
      </div>

    </div>
    </>
  )
}

export default App
