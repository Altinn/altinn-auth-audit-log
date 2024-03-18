function getVersion()
{
    let currentDate = new Date();
    let year = currentDate.getFullYear();
    let month = currentDate.getMonth() + 1;
    let day = currentDate.getDate();
    
    let release_version_number = `${year}.${month}.${day}`;
    return release_version_number;
}

console.log(getVersion());
