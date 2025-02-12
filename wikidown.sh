read_wiki() {
    encoded2=$(echo $2 | jq -Rr '@uri')
    encoded2=$(echo $encoded2 | sed 's#%26#\&#g; s#%3D#=#g')
    # Use curl to fetch the data from the URL
    response=$(curl -s "https://$1/w/api.php?$encoded2" 2>&1)

    # Check if curl command was successful
    if [ $? -eq 0 ]; then
        echo "$response"
    else
        echo "Couldn't connect to \"https://$1/w/api.php?$2\": $response"
    fi
}

echo "Enter arguments for page content. The prop parsewarnings will not be saved if empty, but will warn you if not empty, so it's recommended. This app is not meant to work with deprecated options. (utf8=true&prop=wikitext|categories|revid|properties|parsewarnings)"
read -p "" pageContentArguments
if [[ "$pageContentArguments" == "" ]]; then
    pageContentArguments="utf8=true&prop=wikitext|categories|revid|properties|parsewarnings"
fi
echo "Enter the base wiki URLs and prefixes. Ex: en.wikipedia.org:Prefix,incubator.wikimedia.org:Wp/ess/."
read -p "" parameters

IFS=',' read -r -a parameterPairs <<< "$parameters"
for ((i=0; i<${#parameterPairs[@]}; i++)); do
    if [ "$i" -ne 0 ]; then
        echo -ne "\r${parameterPairs[i-1]} - completed    \n"
    fi

    if [[ "${parameterPairs[i]}" == *":"* ]]; then
        IFS=':' read -r baseURL prefix <<< "${parameterPairs[i]}"
    else
        baseURL="incubator.wikimedia.org"
        prefix="${parameterPairs[i]}"
    fi

    # echo $(read_wiki "$baseURL" "action=query&format=json&list=allpages&aplimit=max&apfilterredir=nonredirects&apprefix=Wp/akz&apcontinue=Wp/akz/Bréhéville")
    # encoded2=$(echo "action=query&format=json&list=allpages&aplimit=max&apfilterredir=nonredirects&apprefix=Wp/akz&apcontinue=Wp/akz/Bréhéville" | jq -Rr '@uri')
    # encoded2=$(echo $encoded2 | sed 's#%26#\&#g; s#%3D#=#g')
    # echo "https://$baseURL/w/api.php?$encoded2"

    # Replace '/' with '-' in the prefix
    safePrefix=$(echo "$prefix" | tr '/' '-')

    # Create the directory
    mkdir -p "output/$baseURL/$safePrefix"
    echo -ne "${parameterPairs[i]} - listing pages"

    download_pages() {
        apcontinue=$1

        listArguments="action=query&format=json&list=allpages&aplimit=max&apfilterredir=nonredirects&apprefix=$prefix"
        if [ -n "$apcontinue" ]; then
            listArguments+="&apcontinue=$apcontinue"
        fi
        pages=$(read_wiki "$baseURL" "$listArguments")

        pagesJSON=$(echo "$pages" | jq .)
        if [[ -z "$pagesJSON" || $(echo "$pagesJSON" | jq -r 'type') == "string" ]]; then
            echo -e "\n\033[41mFatal error: Attempt to read Wikipedia API: https://$baseURL/w/api.php?$listArguments\nreturned invalid value: $pages\033[0m"
            return
        fi

        echo -ne "\r${parameterPairs[i]} - downloading  \033[2D"

        # Iterate over pages
        echo "$pagesJSON" | jq -c '.query.allpages[]' | while read -r page; do
            pageid=$(echo "$page" | jq -r '.pageid')
            title=$(echo "$page" | jq -r '.title')

            pageData=$(read_wiki "$baseURL" "action=parse&format=json&pageid=$pageid&$pageContentArguments" | sed 's/\\n/\n/g')

            if [[ "$pageContentArguments" == *"parsewarnings"* ]]; then
                if [[ "$pageData" == *'"parsewarnings":[]'* ]]; then
                    pageData=$(echo "$pageData" | sed 's/"parsewarnings":\[\],//')
                else
                    echo -e "\n\033[43;30mWarning: page $title($pageid) seems to contain parse warnings. See its file for their content.\033[0m"
                fi
            fi

            filename="output/$baseURL/$safePrefix/$(echo "$title" | tr '/' '-').json"
            if [[ -f "$filename" ]]; then
                filename="output/$baseURL/$safePrefix/$pageid.json"
                if [[ -f "$filename" ]]; then
                    echo -e "\n\033[41mError: Didn't download page with the id $pageid: File already exists. Continuing download of further pages.\033[0m"
                    continue
                fi
            fi

            echo "$pageData" > $filename
        done

        if [[ $(echo "$pagesJSON" | jq -r '.continue.apcontinue') != "null" ]]; then
            download_pages "$(echo "$pagesJSON" | jq -r '.continue.apcontinue')"
        fi
    }

    download_pages
done

echo -ne "\r${parameterPairs[-1]} - completed    "
echo -ne "\nDownloaded all requested pages."
read -r