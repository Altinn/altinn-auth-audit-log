function getVersion()
{
    const currentDate = new Date();
    const year = String(currentDate.getFullYear()).padStart(4, '0');
    const month = String(currentDate.getMonth() + 1).padStart(2, '0');
    const day = String(currentDate.getDate()).padStart(2, '0');
    
    const release_version_number = `${year}.${month}.${day}`;
    return release_version_number;
}

console.log(getVersion());
