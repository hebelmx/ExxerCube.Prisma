// Function to download files from the browser
// Supports both data URIs and blob content
window.downloadFile = function (content, fileName, contentType) {
    let url;
    
    // Check if content is a data URI
    if (typeof content === 'string' && content.startsWith('data:')) {
        url = content;
    } else {
        // Create blob from content
        const blob = new Blob([content], { type: contentType });
        url = window.URL.createObjectURL(blob);
    }
    
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    
    // Only revoke if we created an object URL
    if (!content.startsWith('data:')) {
        window.URL.revokeObjectURL(url);
    }
};
