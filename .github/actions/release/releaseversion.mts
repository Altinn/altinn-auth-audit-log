function getVersion()
{
    const currentDate = new Date();
    const year = currentDate.getFullYear();
    const month = currentDate.getMonth() + 1;
    const day = currentDate.getDate();
    
    const release_version_number = `${year}.${month}.${day}`;
    return release_version_number;
}

console.log(getVersion());
