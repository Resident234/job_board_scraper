DELETE FROM habr_resumes a
USING habr_resumes b
WHERE a.ctid < b.ctid
  AND a.link = b.link