// Reads the first file from an <input type="file"> element and returns text
window.FileHelpers = {
    readAsTextFromInput: (input) => new Promise((resolve, reject) => {
        try {
            const file = input?.files?.[0];
            if (!file) { resolve(""); return; }
            const reader = new FileReader();
            reader.onload = () => resolve(reader.result);
            reader.onerror = () => reject(reader.error);
            reader.readAsText(file);
        } catch (err) { reject(err); }
    })
};
